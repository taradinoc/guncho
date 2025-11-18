using Guncho.Data;
using Guncho.Repositories;
using Guncho.Services;
using Guncho.WebHost.Configuration;
using Guncho.WebHost.Hubs;
using Guncho.WebHost.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Ensure static web assets (Blazor client) are available when running locally
builder.WebHost.UseStaticWebAssets();

// Add database context
builder.Services.AddDbContext<GunchoDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("GunchoDatabase");
    options.UseSqlite(connectionString);
}, ServiceLifetime.Transient); // Use Transient for thread-safety with concurrent access

// Add repositories
builder.Services.AddScoped<PlayerRepository>();
builder.Services.AddScoped<RealmRepository>();
builder.Services.AddScoped<RealmAssetRepository>();
builder.Services.AddScoped<StorageRepository>();

// Add services
builder.Services.AddControllers()
    .AddNewtonsoftJson();
builder.Services.AddSignalR();

// Add authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Guncho.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        };
    });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Register Guncho configuration and services
builder.Services.AddSingleton<IServerConfiguration>(sp => 
    new GunchoConfiguration(builder.Configuration));
builder.Services.AddSingleton<Guncho.ILogger, ConsoleLogger>();
builder.Services.AddSingleton<ISignalRConnectionManager, SignalRConnectionManager>();

// Register core game services - single instance implements multiple interfaces
builder.Services.AddSingleton<GunchoServerServices>();
builder.Services.AddSingleton<IPlayerService>(sp => sp.GetRequiredService<GunchoServerServices>());
builder.Services.AddSingleton<IRealmService>(sp => sp.GetRequiredService<GunchoServerServices>());
builder.Services.AddSingleton<IInstanceService>(sp => sp.GetRequiredService<GunchoServerServices>());
builder.Services.AddSingleton<IConnectionService>(sp => sp.GetRequiredService<GunchoServerServices>());

// Register TCP server as hosted service
builder.Services.AddHostedService<TcpServerHostedService>();

var app = builder.Build();

// Configure middleware pipeline
app.UseCors();

// Enable Blazor WebAssembly framework files serving
app.UseBlazorFrameworkFiles();

// Configure static file options with proper MIME types
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".dat"] = "application/octet-stream";
provider.Mappings[".br"] = "application/x-br";
provider.Mappings[".wasm"] = "application/wasm";
provider.Mappings[".json"] = "application/json";
provider.Mappings[".js"] = "application/javascript";

// Serve default files (index.html) and static files
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider,
    ServeUnknownFileTypes = false
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<PlayHub>("/signalr/play");

// Fallback to index.html for client-side routing
app.MapFallbackToFile("index.html");

// Initialize game server components
// Ensure database file and schema exist before services that query the DB run
try
{
    var connString = builder.Configuration.GetConnectionString("GunchoDatabase");
    if (!string.IsNullOrWhiteSpace(connString))
    {
        var csb = new SqliteConnectionStringBuilder(connString);
        var dbPath = csb.DataSource;
        string? dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GunchoDbContext>();
            await db.Database.EnsureCreatedAsync();
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[DB INIT] Failed to ensure database is created: {ex.Message}");
    throw;
}

var serverServices = app.Services.GetRequiredService<GunchoServerServices>();
await serverServices.InitializeAsync();

app.Run();
