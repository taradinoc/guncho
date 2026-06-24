using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Guncho.Services;

namespace Guncho
{
    public partial class Inform6RealmFactory
    {
        private static readonly string[] inform6BinsWin = { "inform6.exe" };
        private static readonly string[] inform6BinsUnix = { "inform6", "inform6.exe" };

        public static Inform6RealmFactory[] ConstructAll(IServerConfiguration config, Microsoft.Extensions.Logging.ILogger logger, string compilerPath, string libraryPath, string indexOutputDir)
        {
            var factories = new List<Inform6RealmFactory>();
            
            // If compilerPath is a directory, search for platform-specific binary
            string? actualCompilerPath;
            if (Directory.Exists(compilerPath))
            {
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                var candidates = isWindows ? inform6BinsWin : inform6BinsUnix;
                
                actualCompilerPath = null;
                foreach (var candidate in candidates)
                {
                    var fullPath = Path.Combine(compilerPath, candidate);
                    if (File.Exists(fullPath))
                    {
                        actualCompilerPath = fullPath;
                        break;
                    }
                }
                
                if (actualCompilerPath == null)
                {
                    logger.LogWarning($"Inform 6 compiler not found in directory: {compilerPath}");
                    return factories.ToArray();
                }
            }
            else if (File.Exists(compilerPath))
            {
                actualCompilerPath = compilerPath;
            }
            else
            {
                logger.LogWarning($"Inform 6 compiler not found: {compilerPath}");
                return factories.ToArray();
            }
            
            if (Directory.Exists(libraryPath))
            {
                factories.Add(new Inform6RealmFactory(
                    config: config,
                    logger: logger,
                    name: "Inform 6",
                    inform6CompilerPath: actualCompilerPath,
                    inform6LibraryDir: libraryPath,
                    indexOutputDir: indexOutputDir));
            }
            else
            {
                logger.LogWarning($"Inform 6 library not found: {libraryPath}");
            }
            
            return factories.ToArray();
        }
    }
}
