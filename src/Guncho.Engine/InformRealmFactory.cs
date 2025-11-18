using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Guncho.Services;

namespace Guncho
{
    public sealed class InformRealmFactory : RealmFactory
    {
        private readonly ILogger logger;

        private readonly string niCompilerPath, niExtensionDir;
        private readonly string infCompilerPath, infLibraryDir;
        private readonly string indexOutputDir;

        public InformRealmFactory(IServerConfiguration config, ILogger logger, string name,
            string niCompilerPath, string niExtensionDir,
            string infCompilerPath, string infLibraryDir,
            string indexOutputDir)
            : base(name, config)
        {
            this.logger = logger;
            this.niCompilerPath = niCompilerPath;
            this.niExtensionDir = niExtensionDir;
            this.infCompilerPath = infCompilerPath;
            this.infLibraryDir = infLibraryDir;
            this.indexOutputDir = indexOutputDir;
        }

        public string NiCompilerPath => niCompilerPath;

        public string NiExtensionDirectory => niExtensionDir;

        public string Inform6CompilerPath => infCompilerPath;

        public string Inform6LibraryDirectory => infLibraryDir;

        public string IndexOutputDirectory => indexOutputDir;

        public override string SourceFileExtension
        {
            get { return ".ni"; }
        }

        public override string Description => $"Inform 7 version {Name} with Glulx support";

        public override string GetInitialSourceText(string ownerName, string realmName)
        {
            StringBuilder sb = new StringBuilder(100);

            sb.Append('"'); sb.Append(realmName); sb.Append('"');
            if (ownerName != null && ownerName.Length > 0)
            {
                sb.Append(" by ");
                sb.Append(ownerName);
            }
            sb.AppendLine();

            sb.AppendLine();
            sb.AppendLine("[TODO: Replace this with your own Inform 7 code.]");
            sb.AppendLine("Home is a room.");

            return sb.ToString();
        }

        [Obsolete("Use CompileRealmAsync with assets dictionary instead")]
        public override async Task<RealmEditingOutcome> CompileRealmAsync(string realmName, string sourceFile, string outputFile)
        {
            string skeleton = config.NiSkeletonPath;
            string uuid = MakeUUID(realmName);
            File.WriteAllText(Path.Combine(skeleton, "uuid.txt"), uuid);

            string tempNI = Path.Combine(skeleton, "Source" + Path.DirectorySeparatorChar + "story.ni");
            string tempINF = Path.Combine(skeleton, "Build" + Path.DirectorySeparatorChar + "auto.inf");
            File.Delete(tempNI);
            File.Delete(tempINF);
            File.Copy(sourceFile, tempNI);

            string output = await ExecuteAsync(niCompilerPath, config.CompilerTimeout, config.NiSkeletonPath,
                "-release",
                "-rules", niExtensionDir,
                "-package", skeleton,
                "-extension=ulx");

            if (!Directory.Exists(indexOutputDir))
                Directory.CreateDirectory(indexOutputDir);

            // copy Index
            string realmIndexPath = Path.Combine(indexOutputDir, realmName);

            CopyDirectory(
                Path.Combine(skeleton, "Index"),
                realmIndexPath);

            // detect compiler hanging
            if (output == null)
            {
                using (StreamWriter wtr = new StreamWriter(
                    Path.Combine(realmIndexPath, "Problems.html")))
                {
                    wtr.WriteLine("<font size=\"2\">");
                    wtr.WriteLine("<p><b>Inform 7 compiler failed</b></p>");
                    wtr.WriteLine("<p>The Inform 7 compiler hung while trying to compile this realm.");
                    wtr.WriteLine("</font>");
                }

                logger.LogMessage(LogLevel.Warning, "NI hung while compiling '{0}'", realmName);

                return RealmEditingOutcome.NiError;
            }
            else
            {
                // copy Problems.html
                File.Copy(
                    Path.Combine(skeleton, "Build" + Path.DirectorySeparatorChar + "Problems.html"),
                    Path.Combine(realmIndexPath, "Problems.html"),
                    true);

                if (File.Exists(tempINF) && output.Contains("source text has successfully been translated"))
                {
                    // Linux I7 adds a blank line at the top of auto.inf that breaks ICL parsing
                    string tempContent = File.ReadAllText(tempINF);
                    int i = 0;
                    while (i < tempContent.Length && char.IsWhiteSpace(tempContent[i]))
                        i++;
                    if (i > 0 && i < tempContent.Length)
                        File.WriteAllText(tempINF, tempContent.Substring(i));

                    output = await ExecuteAsync(infCompilerPath, config.CompilerTimeout, config.NiSkeletonPath,
                        "-Gw",
                        "+include_path=" + infLibraryDir,
                        tempINF,
                        outputFile);

                    if (output == null)
                    {
                        using (StreamWriter wtr = new StreamWriter(
                            Path.Combine(realmIndexPath, "Problems.html")))
                        {
                            wtr.WriteLine("<font size=\"2\">");
                            wtr.WriteLine("<p><b>Inform 6 compiler failed</b></p>");
                            wtr.WriteLine("<p>The Inform 6 compiler hung while trying to compile this realm.");
                            wtr.WriteLine("</font>");
                        }

                        logger.LogMessage(LogLevel.Warning, "Inform 6 hung while compiling '{0}'", realmName);

                        return RealmEditingOutcome.InfError;
                    }
                    else
                    {
                        Regex errorRegex = new Regex(@"Compiled with \d+ error");
                        if (errorRegex.IsMatch(output))
                        {
                            using (StreamWriter wtr = new StreamWriter(
                                Path.Combine(realmIndexPath, "Problems.html")))
                            {
                                wtr.WriteLine("<font size=\"2\">");
                                wtr.WriteLine("<p><b>Translated to Inform 6 but failed to compile</b></p>");
                                wtr.WriteLine("<pre>");

                                Regex filenameRegex = new Regex(@"^[^>].*?auto.inf", RegexOptions.Multiline);
                                output = filenameRegex.Replace(output, "auto.inf");
                                wtr.Write(System.Net.WebUtility.HtmlEncode(output));

                                wtr.WriteLine("</pre></font>");
                            }

                            logger.LogMessage(LogLevel.Warning, "Inform 6 compiler output for '{0}': {1}", realmName, TruncateCompilerOutput(output));
                            return RealmEditingOutcome.InfError;
                        }

                        // OK!
                        return RealmEditingOutcome.Success;
                    }
                }
                else
                {
                    logger.LogMessage(LogLevel.Warning, "Inform 7 compiler output for '{0}': {1}", realmName, TruncateCompilerOutput(output));
                    return RealmEditingOutcome.NiError;
                }
            }
        }

        private static string TruncateCompilerOutput(string? text)
        {
            const int limit = 4000;
            if (string.IsNullOrWhiteSpace(text))
                return "<no output>";

            if (text!.Length <= limit)
                return text;

            return text.Substring(0, limit) + "... (truncated)";
        }

    private static readonly string[] niBinsWin = ["ni.exe"];
    private static readonly string[] niBinsUnix = ["ni", "ni.exe"];
    private static readonly string[] i6BinsWin = ["inform-631.exe"];
    private static readonly string[] i6BinsUnix = ["inform-6.31-biplatform", "inform-631.exe"];

        private static bool FindCompilers(string dir, [NotNullWhen(true)] out string? nibin, [NotNullWhen(true)] out string? i6bin)
        {
            nibin = i6bin = null;

            if (Directory.Exists(dir))
            {
                var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
                var niCandidates = isWindows ? niBinsWin : niBinsUnix;
                var i6Candidates = isWindows ? i6BinsWin : i6BinsUnix;

                foreach (string name in niCandidates)
                {
                    if (File.Exists(Path.Combine(dir, name)))
                    {
                        nibin = Path.Combine(dir, name);
                        break;
                    }
                }

                foreach (string name in i6Candidates)
                {
                    if (File.Exists(Path.Combine(dir, name)))
                    {
                        i6bin = Path.Combine(dir, name);
                        break;
                    }
                }
            }

            return (nibin != null) && (i6bin != null);
        }

        public static InformRealmFactory[] ConstructAll(IServerConfiguration config, ILogger logger, string installationsPath, string indexOutputDir)
        {
            var result = new List<InformRealmFactory>();

            foreach (string subPath in Directory.GetDirectories(installationsPath))
            {
                if (FindCompilers(Path.Combine(subPath, "Compilers"), out var nibin, out var i6bin))
                {
                    var version = Path.GetFileName(subPath);

                    var factory = new InformRealmFactory(
                        config: config,
                        logger: logger,
                        name: version,
                        niCompilerPath: nibin,
                        niExtensionDir: Path.Combine(subPath, "Inform7", "Extensions"),
                        infCompilerPath: i6bin,
                        infLibraryDir: Path.Combine(subPath, "Library", "Natural"),
                        indexOutputDir: indexOutputDir);

                    result.Add(factory);
                }
            }

            return result.ToArray();
        }

        public override IInstance LoadInstance(IInstanceSite site, Realm realm, string name, ILogger logger)
        {
            FileStream stream = new FileStream(realm.StoryFile, FileMode.Open, FileAccess.Read);
            return new FyreVMInstance(site, config, realm, stream, name, logger);
        }
    }
}
