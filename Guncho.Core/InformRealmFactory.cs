using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Guncho
{
    class InformRealmFactory : RealmFactory
    {
        private readonly ILogger logger;

        private readonly string niCompilerPath, niExtensionDir;
        private readonly string infCompilerPath, infLibraryDir;
        private readonly string indexOutputDir;

        public InformRealmFactory(ILogger logger, string name,
            string niCompilerPath, string niExtensionDir,
            string infCompilerPath, string infLibraryDir,
            string indexOutputDir)
            : base(name)
        {
            this.logger = logger;
            this.niCompilerPath = niCompilerPath;
            this.niExtensionDir = niExtensionDir;
            this.infCompilerPath = infCompilerPath;
            this.infLibraryDir = infLibraryDir;
            this.indexOutputDir = indexOutputDir;
        }

        public override string SourceFileExtension
        {
            get { return ".ni"; }
        }

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

        public override RealmEditingOutcome CompileRealm(string realmName, string sourceFile, string outputFile)
        {
            string skeleton = Properties.Settings.Default.NiSkeletonPath;
            string uuid = MakeUUID(realmName);
            File.WriteAllText(Path.Combine(skeleton, "uuid.txt"), uuid);

            string tempNI = Path.Combine(skeleton, "Source" + Path.DirectorySeparatorChar + "story.ni");
            string tempINF = Path.Combine(skeleton, "Build" + Path.DirectorySeparatorChar + "auto.inf");
            File.Delete(tempNI);
            File.Delete(tempINF);
            File.Copy(sourceFile, tempNI);

            string output = Execute(niCompilerPath,
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

                    output = Execute(infCompilerPath,
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
                                HttpUtility.HtmlEncode(output, wtr);

                                wtr.WriteLine("</pre></font>");
                            }

                            return RealmEditingOutcome.InfError;
                        }

                        // OK!
                        return RealmEditingOutcome.Success;
                    }
                }
                else
                {
                    return RealmEditingOutcome.NiError;
                }
            }
        }

        // TODO: Choose the right executable based on platform, instead of using the first available file.
        private static readonly string[] niBins = { "ni", "ni.exe" };
        private static readonly string[] i6Bins = { "inform-6.31-biplatform", "inform-631.exe" };

        private static bool FindCompilers(string dir, out string nibin, out string i6bin)
        {
            nibin = i6bin = null;

            if (Directory.Exists(dir))
            {
                foreach (string name in niBins)
                {
                    if (File.Exists(Path.Combine(dir, name)))
                    {
                        nibin = Path.Combine(dir, name);
                        break;
                    }
                }

                foreach (string name in i6Bins)
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

        internal static InformRealmFactory[] ConstructAll(ILogger logger, string installationsPath, string indexOutputDir)
        {
            var result = new List<InformRealmFactory>();

            foreach (string subPath in Directory.GetDirectories(installationsPath))
            {
                string nibin, i6bin;
                if (FindCompilers(Path.Combine(subPath, "Compilers"), out nibin, out i6bin))
                {
                    var version = Path.GetFileName(subPath);

                    var factory = new InformRealmFactory(
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
    }
}
