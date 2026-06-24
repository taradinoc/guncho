using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Blazored.LocalStorage;
using Guncho.Client;
using Guncho.Client.Auth;
using Guncho.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// In production, BaseAddress is the server. In dev with separate client, use API server.
var baseAddress = builder.HostEnvironment.BaseAddress;

// Register the auth message handler that detects 401 responses and clears stale auth state.
builder.Services.AddScoped<AuthMessageHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthMessageHandler>();
    return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
});

// Add authentication
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<RealmsService>();
builder.Services.AddScoped<RealmAssetsService>();
builder.Services.AddScoped<PlayConnectionService>();

await builder.Build().RunAsync();
