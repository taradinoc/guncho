using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Guncho.Services;

namespace Guncho
{
    // Inform 6 realm factory for Guncho
    public sealed partial class Inform6RealmFactory : RealmFactory
    {
        private readonly Microsoft.Extensions.Logging.ILogger logger;
        private readonly string inform6CompilerPath;
        private readonly string inform6LibraryDir;
        private readonly string indexOutputDir;

        public Inform6RealmFactory(IServerConfiguration config, Microsoft.Extensions.Logging.ILogger logger, string name,
            string inform6CompilerPath, string inform6LibraryDir, string indexOutputDir)
            : base(name, config)
        {
            this.logger = logger;
            this.inform6CompilerPath = inform6CompilerPath;
            this.inform6LibraryDir = inform6LibraryDir;
            this.indexOutputDir = indexOutputDir;
        }

        public string Inform6CompilerExecutable => inform6CompilerPath;

        public string Inform6LibraryDirectory => inform6LibraryDir;

        public override string SourceFileExtension => ".inf";

        public override string Description => "Inform 6 with Glulx support";

        public override string DefaultMainFileName => "main.inf";

        public override string GetInitialSourceText(string ownerName, string realmName)
        {
            // Provide a basic Inform 6 template
            return $@"Constant Story ""{realmName}"";
Constant Headline ""An Inform 6 realm for Guncho."";

Include ""Parser"";
Include ""VerbLib"";

[ Initialise;
    location = Room1;
    print ""Welcome to {realmName}, created by {ownerName}.^"";
];

Object Room1 ""Room1""
    with description ""A plain room."",
    has light;

Include ""Grammar"";
";
        }

        [Obsolete("Use CompileRealmAsync with assets dictionary instead")]
        public override async Task<RealmEditingOutcome> CompileRealmAsync(string realmName, string sourceFile, string outputFile)
        {
            // Compile using inform6.exe
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = inform6CompilerPath;
            process.StartInfo.Arguments = $"-w -G \"{sourceFile}\" \"{outputFile}\" +include_path=\"{inform6LibraryDir}\"";
            var wd = Path.GetDirectoryName(sourceFile);
            if (!string.IsNullOrEmpty(wd))
                process.StartInfo.WorkingDirectory = wd;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            try
            {
                process.Start();
                string stdout = await process.StandardOutput.ReadToEndAsync();
                string stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                string output = stdout + stderr;

                // Check for compilation errors
                if (process.ExitCode != 0)
                {
                    logger.LogError($"Inform 6 compilation failed for '{realmName}': {output}");
                    return RealmEditingOutcome.InfError;
                }

                if (!File.Exists(outputFile))
                {
                    logger.LogError($"Inform 6 compilation succeeded but output file not found: '{outputFile}'");
                    return RealmEditingOutcome.InfError;
                }

                logger.LogDebug($"Successfully compiled Inform 6 realm '{realmName}'");
                return RealmEditingOutcome.Success;
            }
            catch (Exception ex)
            {
                logger.LogError($"Exception during Inform 6 compilation of '{realmName}': {ex.Message}");
                return RealmEditingOutcome.InfError;
            }
        }

        public override IInstance LoadInstance(IInstanceSite site, Realm realm, string name, Microsoft.Extensions.Logging.ILogger logger)
        {
            // Inform 6 compiles to Glulx (.ulx), which runs on FyreVM just like Inform 7
            FileStream stream = new FileStream(realm.StoryFile, FileMode.Open, FileAccess.Read);
            return new FyreVMInstance(site, config, realm, stream, name, logger);
        }
    }
}
