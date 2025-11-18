using Guncho;
using Guncho.Data;
using Guncho.Services;
using Microsoft.EntityFrameworkCore;

namespace Guncho.Migrator;

/// <summary>
/// Console application to migrate Guncho data from XML files to SQLite database.
/// Run this once before deploying the new version.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Guncho XML to SQLite Migration Tool");
        Console.WriteLine("====================================");
        Console.WriteLine();

        // Parse command-line arguments
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: Guncho.Migrator <RealmDataPath> <DatabasePath>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  RealmDataPath  - Directory containing XML files (playerIndex.xml, realmIndex.xml, etc.)");
            Console.WriteLine("  DatabasePath   - Output SQLite database file path (e.g., guncho.db)");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  Guncho.Migrator C:\\guncho\\RealmData C:\\guncho\\RealmData\\guncho.db");
            Console.WriteLine("  Guncho.Migrator /app/RealmData /app/RealmData/guncho.db");
            return;
        }

        var realmDataPath = args[0];
        var dbPath = args[1];

        // Normalize paths
        realmDataPath = Path.GetFullPath(realmDataPath);
        dbPath = Path.GetFullPath(dbPath);

        Console.WriteLine($"Realm Data Path: {realmDataPath}");
        Console.WriteLine($"Database Path: {dbPath}");
        Console.WriteLine();

        // Check if RealmData directory exists
        if (!Directory.Exists(realmDataPath))
        {
            Console.Error.WriteLine($"ERROR: RealmData directory not found at: {realmDataPath}");
            Console.Error.WriteLine("Please provide a valid path to the RealmData directory.");
            return;
        }

        // Check if database directory exists, create if needed
        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
        {
            Console.WriteLine($"Creating directory: {dbDirectory}");
            Directory.CreateDirectory(dbDirectory);
        }

        // Ask for confirmation
        Console.Write("This will create a new SQLite database and import all XML data. Continue? (y/n): ");
        var response = Console.ReadLine()?.Trim().ToLower();
        if (response != "y" && response != "yes")
        {
            Console.WriteLine("Migration cancelled.");
            return;
        }
        Console.WriteLine();

        // Set up database context
        var optionsBuilder = new DbContextOptionsBuilder<GunchoDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

        using var dbContext = new GunchoDbContext(optionsBuilder.Options);
        await dbContext.Database.EnsureCreatedAsync();

        // Create logger
        var logger = new ConsoleLogger();

        // Run migration
        try
        {
            var migrator = new XmlToSqliteMigration(dbContext, realmDataPath, logger);
            await migrator.MigrateAsync();

            Console.WriteLine();
            Console.WriteLine("Migration completed successfully!");
            Console.WriteLine();
            Console.WriteLine("IMPORTANT: The XML files are still in place for backup purposes.");
            Console.WriteLine("The new SQLite database will be used instead when you run the server.");

            // Optional verification summary
            Console.WriteLine();
            await DbVerifier.VerifyAsync(dbPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"ERROR: Migration failed: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.ExitCode = 1;
        }
    }
}

/// <summary>
/// Simple console logger implementation.
/// </summary>
class ConsoleLogger : ILogger
{
    public void LogMessage(LogLevel level, string text)
    {
        var color = level switch
        {
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Notice => ConsoleColor.Green,
            LogLevel.Verbose => ConsoleColor.Gray,
            _ => ConsoleColor.White
        };

        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"[{level}] {text}");
        Console.ForegroundColor = oldColor;
    }
}
