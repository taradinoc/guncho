using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Guncho;
using Guncho.Data;
using Guncho.Repositories;
using Guncho.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Guncho.Tools;

internal static class Program
{
    private const string DefaultAppSettings = "appsettings.json";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();
        var remaining = args.Skip(1).ToArray();

        return command switch
        {
            "realm-sim" or "realm-build" or "realm-compile" => await RunRealmSimulationAsync(remaining),
            _ => UnknownCommand(command)
        };
    }

    private static bool IsHelp(string arg) => arg is "-h" or "--help" or "help";

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Guncho.Tools");
        Console.WriteLine("============");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  Guncho.Tools realm-sim --realm <RealmName> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --appsettings <path>     Path to appsettings JSON (default: appsettings.json if present)");
        Console.WriteLine("  --connection <string>    Override database connection string");
        Console.WriteLine("  --output <path>          Directory to place extracted files (default: temp)");
        Console.WriteLine("  --cleanup                Delete the temporary directory after running");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  Guncho.Tools realm-sim --realm \"The Outer Realm\"");
    }

    private static async Task<int> RunRealmSimulationAsync(string[] args)
    {
        RealmSimOptions options;
        try
        {
            options = RealmSimOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            PrintUsage();
            return 1;
        }

        var configuration = BuildConfiguration(options.AppSettingsPath ?? DefaultAppSettings);
        var serverConfig = new CliServerConfiguration(configuration);
        var logger = new ConsoleLogger();
        var simulator = new RealmBuildSimulator(configuration, serverConfig, logger);
        return await simulator.RunAsync(options);
    }

    private static IConfigurationRoot BuildConfiguration(string? appSettingsPath)
    {
        var builder = new ConfigurationBuilder();

        if (!string.IsNullOrWhiteSpace(appSettingsPath))
        {
            var fullPath = Path.GetFullPath(appSettingsPath);
            if (File.Exists(fullPath))
            {
                builder.AddJsonFile(fullPath, optional: false, reloadOnChange: false);
            }
        }
        else
        {
            var defaultPath = Path.Combine(AppContext.BaseDirectory, DefaultAppSettings);
            if (File.Exists(defaultPath))
            {
                builder.AddJsonFile(defaultPath, optional: true, reloadOnChange: false);
            }
        }

        builder.AddEnvironmentVariables();
        return builder.Build();
    }
}

internal sealed class RealmBuildSimulator
{
    private readonly IConfiguration _configuration;
    private readonly IServerConfiguration _serverConfig;
    private readonly ILogger _logger;

    public RealmBuildSimulator(IConfiguration configuration, IServerConfiguration serverConfig, ILogger logger)
    {
        _configuration = configuration;
        _serverConfig = serverConfig;
        _logger = logger;
    }

    public async Task<int> RunAsync(RealmSimOptions options)
    {
        var connectionString = options.ConnectionString
            ?? _configuration.GetConnectionString("GunchoDatabase")
            ?? $"Data Source={Path.Combine(_serverConfig.RealmDataPath, "guncho.db")}";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("Unable to determine database connection string. Use --connection to override.");
            return 1;
        }

        var dbOptions = new DbContextOptionsBuilder<GunchoDbContext>()
            .UseSqlite(connectionString)
            .Options;

        using var dbContext = new GunchoDbContext(dbOptions);
        var realmRepo = new RealmRepository(dbContext);
        var assetRepo = new RealmAssetRepository(dbContext);

        var metadata = await realmRepo.GetByNameAsync(options.RealmName);
        if (metadata == null)
        {
            Console.Error.WriteLine($"Realm '{options.RealmName}' was not found in the database.");
            return 1;
        }

        var assets = await assetRepo.GetAllContentForRealmAsync(metadata.Id);
        if (assets.Count == 0)
        {
            Console.Error.WriteLine($"Realm '{metadata.Name}' does not have any stored assets to compile.");
            return 1;
        }

        var factories = LoadFactories();
        var factory = factories.FirstOrDefault(f => string.Equals(f.Name, metadata.Factory, StringComparison.OrdinalIgnoreCase));
        if (factory == null)
        {
            Console.Error.WriteLine($"Compiler '{metadata.Factory}' is not installed. Available: {string.Join(", ", factories.Select(f => f.Name))}");
            return 1;
        }

        var scratchRoot = PrepareScratchRoot(options, metadata.Name);
        var assetsRoot = Path.Combine(scratchRoot, "assets");
        Directory.CreateDirectory(assetsRoot);
        WriteAssets(assetsRoot, assets);

        var mainFile = DetermineMainFile(metadata, assets.Keys, factory);
        var mainFilePath = Path.Combine(assetsRoot, NormalizeAssetPath(mainFile));
        if (!File.Exists(mainFilePath))
        {
            Console.Error.WriteLine($"Main file '{mainFile}' was not found after extraction.");
            return 1;
        }

        CompilerCommand[] commands;
        string? skeletonPath = null;
        string outputUlx;

        switch (factory)
        {
            case InformRealmFactory informFactory:
                (commands, skeletonPath, outputUlx) = PrepareInform7Simulation(informFactory, metadata.Name, mainFilePath, scratchRoot);
                break;
            case Inform6RealmFactory inform6Factory:
                (commands, outputUlx) = PrepareInform6Simulation(inform6Factory, metadata.Name, mainFilePath, scratchRoot);
                break;
            default:
                Console.Error.WriteLine($"Factory '{factory.Name}' is not supported by this tool yet.");
                return 1;
        }

        Console.WriteLine();
        Console.WriteLine($"Realm: {metadata.Name}");
        Console.WriteLine($"Compiler: {metadata.Factory}");
        Console.WriteLine($"Temp root: {scratchRoot}");
        Console.WriteLine($"Assets: {assetsRoot}");
        if (!string.IsNullOrEmpty(skeletonPath))
        {
            Console.WriteLine($"Isolated skeleton: {skeletonPath}");
        }
        Console.WriteLine($"Main file: {mainFilePath}");
        Console.WriteLine($"Output (if you run both commands): {outputUlx}");
        Console.WriteLine();
        Console.WriteLine("Commands to reproduce the compile:");
        for (int i = 0; i < commands.Length; i++)
        {
            Console.WriteLine($"{i + 1}. {commands[i].ToDisplayString()}");
        }

        Console.WriteLine();
        if (options.Cleanup)
        {
            try
            {
                Directory.Delete(scratchRoot, true);
                Console.WriteLine("Temporary directory cleaned up.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: failed to delete temp directory: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Temporary files have been left in place for inspection.");
        }

        return 0;
    }

    private (CompilerCommand[] commands, string skeletonPath, string outputUlx) PrepareInform7Simulation(InformRealmFactory factory, string realmName, string mainFilePath, string scratchRoot)
    {
        var skeletonCopy = Path.Combine(scratchRoot, "skeleton");
        RealmFactory.CopyDirectory(_serverConfig.NiSkeletonPath, skeletonCopy);

        var uuidPath = Path.Combine(skeletonCopy, "uuid.txt");
        File.WriteAllText(uuidPath, ComputeRealmUuid(realmName));

        var skeletonStory = Path.Combine(skeletonCopy, "Source", "story.ni");
        Directory.CreateDirectory(Path.GetDirectoryName(skeletonStory)!);
        File.Copy(mainFilePath, skeletonStory, overwrite: true);

        var autoInf = Path.Combine(skeletonCopy, "Build", "auto.inf");
        if (File.Exists(autoInf))
        {
            File.Delete(autoInf);
        }

        var outputUlx = Path.Combine(scratchRoot, $"{SanitizeFileName(realmName)}.ulx");

        var commands = new[]
        {
            new CompilerCommand(
                factory.NiCompilerPath,
                new[]
                {
                    "-release",
                    "-rules", factory.NiExtensionDirectory,
                    "-package", skeletonCopy,
                    "-extension=ulx"
                },
                skeletonCopy,
                new Dictionary<string, string> { ["HOME"] = skeletonCopy }
            ),
            new CompilerCommand(
                factory.Inform6CompilerPath,
                new[]
                {
                    "-Gw",
                    "+include_path=" + factory.Inform6LibraryDirectory,
                    Path.Combine(skeletonCopy, "Build", "auto.inf"),
                    outputUlx
                },
                skeletonCopy,
                new Dictionary<string, string> { ["HOME"] = skeletonCopy }
            )
        };

        return (commands, skeletonCopy, outputUlx);
    }

    private (CompilerCommand[] commands, string outputUlx) PrepareInform6Simulation(Inform6RealmFactory factory, string realmName, string mainFilePath, string scratchRoot)
    {
        var outputUlx = Path.Combine(scratchRoot, $"{SanitizeFileName(realmName)}.ulx");
        var workingDir = Path.GetDirectoryName(mainFilePath) ?? scratchRoot;

        var commands = new[]
        {
            new CompilerCommand(
                factory.Inform6CompilerExecutable,
                new[]
                {
                    "-w",
                    "-G",
                    mainFilePath,
                    outputUlx,
                    "+include_path=" + factory.Inform6LibraryDirectory
                },
                workingDir)
        };

        return (commands, outputUlx);
    }

    private RealmFactory[] LoadFactories()
    {
        var list = new List<RealmFactory>();
        list.AddRange(InformRealmFactory.ConstructAll(_serverConfig, _logger, _serverConfig.NiInstallationsPath, _serverConfig.IndexPath));
        list.AddRange(Inform6RealmFactory.ConstructAll(_serverConfig, _logger, _serverConfig.Inform6CompilerPath, _serverConfig.Inform6LibraryPath, _serverConfig.IndexPath));
        return list.ToArray();
    }

    private static string DetermineMainFile(RealmMetadata metadata, ICollection<string> assetNames, RealmFactory factory)
    {
        var main = TryResolveAssetName(assetNames, metadata.MainFile);
        if (main != null)
        {
            return main;
        }

        var defaultName = factory.DefaultMainFileName.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var defaultMatch = TryResolveAssetName(assetNames, defaultName);
        if (defaultMatch != null)
        {
            return defaultMatch;
        }

        var match = assetNames.FirstOrDefault(name => name.EndsWith(factory.SourceFileExtension, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return match;

        throw new InvalidOperationException($"Unable to determine main file for realm '{metadata.Name}'.");
    }

    private static string? TryResolveAssetName(ICollection<string> assetNames, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        return assetNames.FirstOrDefault(name => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteAssets(string root, IDictionary<string, byte[]> assets)
    {
        foreach (var asset in assets)
        {
            var relative = NormalizeAssetPath(asset.Key);
            var destination = Path.Combine(root, relative);
            var destinationDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            File.WriteAllBytes(destination, asset.Value);
        }
    }

    private static string NormalizeAssetPath(string path)
    {
        var normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return normalized.TrimStart(Path.DirectorySeparatorChar);
    }

    private static string PrepareScratchRoot(RealmSimOptions options, string realmName)
    {
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            var custom = Path.GetFullPath(options.OutputPath);
            Directory.CreateDirectory(custom);
            return custom;
        }

        var slug = SanitizeFileName(realmName);
        var root = Path.Combine(Path.GetTempPath(), $"guncho-realm-{slug}-{DateTime.UtcNow:yyyyMMddHHmmss}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static string ComputeRealmUuid(string realmName)
    {
        using var sha1 = SHA1.Create();
        var bytes = Encoding.UTF8.GetBytes(realmName);
        var hash = sha1.ComputeHash(bytes);
        var sb = new StringBuilder(36);
        for (var i = 0; i < 16; i++)
        {
            if (i is 4 or 6 or 8 or 10)
            {
                sb.Append('-');
            }
            sb.Append(hash[i].ToString("x2"));
        }
        return sb.ToString();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }
        return sb.ToString().Trim('_');
    }
}

internal sealed class RealmSimOptions
{
    public string RealmName { get; init; } = string.Empty;
    public string? AppSettingsPath { get; init; }
    public string? ConnectionString { get; init; }
    public string? OutputPath { get; init; }
    public bool Cleanup { get; init; }

    public static RealmSimOptions Parse(string[] args)
    {
        var realmName = string.Empty;
        string? appSettings = null;
        string? connection = null;
        string? output = null;
        var cleanup = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--realm":
                    realmName = RequireValue(args, ref i, "--realm");
                    break;
                case "--appsettings":
                    appSettings = RequireValue(args, ref i, "--appsettings");
                    break;
                case "--connection":
                    connection = RequireValue(args, ref i, "--connection");
                    break;
                case "--output":
                    output = RequireValue(args, ref i, "--output");
                    break;
                case "--cleanup":
                    cleanup = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(realmName))
        {
            throw new ArgumentException("Missing required --realm <RealmName> option.");
        }

        return new RealmSimOptions
        {
            RealmName = realmName,
            AppSettingsPath = appSettings,
            ConnectionString = connection,
            OutputPath = output,
            Cleanup = cleanup
        };
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Option {option} requires a value.");
        }

        index++;
        return args[index];
    }
}

internal sealed class CliServerConfiguration : IServerConfiguration
{
    private readonly IConfiguration _configuration;

    public CliServerConfiguration(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    private string GetSetting(string key, string fallback) => _configuration[$"Guncho:{key}"] ?? fallback;

    public string CachePath => GetSetting("CachePath", Path.Combine(Environment.CurrentDirectory, "Cache"));
    public string IndexPath => Path.Combine(CachePath, "Index");
    public string RealmDataPath => GetSetting("RealmDataPath", Path.Combine(Environment.CurrentDirectory, "RealmData"));
    public string LogPath => GetSetting("LogPath", Path.Combine(Environment.CurrentDirectory, "Logs"));
    public string NiInstallationsPath => GetSetting("NiInstallationsPath", Path.Combine(Environment.CurrentDirectory, "Factories", "Inform7"));
    public string NiSkeletonPath => GetSetting("NiSkeletonPath", Path.Combine(NiInstallationsPath, "Skeleton.inform"));
    public string StartRealmName => GetSetting("StartRealmName", "The Outer Realm");
    public int CompilerTimeout => int.Parse(GetSetting("CompilerTimeout", "300000"));
    public int TransactionTimeout => int.Parse(GetSetting("TransactionTimeout", "5000"));
    public int RealmFailuresAllowed => int.Parse(GetSetting("RealmFailuresAllowed", "5"));
    public string WebAuthSecret => GetSetting("WebAuthSecret", string.Empty);
    public uint MaxHeapSize => uint.Parse(GetSetting("MaxHeapSize", "33554432"));
    public string MotdFileName => GetSetting("MotdFileName", "motd.txt");
    public string GuestMotdFileName => GetSetting("GuestMotdFileName", "guest.txt");
    public string ConnectTextFileName => GetSetting("ConnectTextFileName", "connect.txt");
    public int GameServerPort => int.Parse(GetSetting("GameServerPort", "4108"));
    public bool FilterBlankLines => bool.TryParse(GetSetting("FilterBlankLines", "false"), out var result) && result;
    public string Inform6CompilerPath => GetSetting("Inform6CompilerPath", Path.Combine(Environment.CurrentDirectory, "Factories", "Inform6"));
    public string Inform6LibraryPath => GetSetting("Inform6LibraryPath", Path.Combine(Inform6CompilerPath, "library"));
    public string MotdPath => Path.Combine(RealmDataPath, MotdFileName);
    public string GuestMotdPath => Path.Combine(RealmDataPath, GuestMotdFileName);
    public string ConnectTextPath => Path.Combine(RealmDataPath, ConnectTextFileName);
}

internal sealed class ConsoleLogger : ILogger
{
    public void LogMessage(LogLevel level, string text)
    {
        Console.WriteLine($"[{level}] {text}");
    }
}

internal sealed record CompilerCommand(string Executable, IReadOnlyList<string> Arguments, string WorkingDirectory, IReadOnlyDictionary<string, string>? Environment = null)
{
    public string ToDisplayString()
    {
        var builder = new StringBuilder();
        if (Environment is { Count: > 0 })
        {
            foreach (var pair in Environment)
            {
                builder.Append(pair.Key);
                builder.Append('=');
                builder.Append(Escape(pair.Value));
                builder.Append(' ');
            }
        }

        builder.Append(Escape(Executable));
        foreach (var arg in Arguments)
        {
            builder.Append(' ');
            builder.Append(Escape(arg));
        }

        builder.Append("  # working dir: ");
        builder.Append(WorkingDirectory);
        return builder.ToString();
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return value.IndexOfAny([' ', '\t', '\"']) >= 0
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
    }
}
