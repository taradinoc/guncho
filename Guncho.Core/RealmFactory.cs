using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Web;

namespace Guncho
{
    public abstract class RealmFactory
    {
        protected readonly Server server;
        private readonly string name;

        protected RealmFactory(Server server, string factoryName)
        {
            this.server = server;
            this.name = factoryName;
        }

        public string Name
        {
            get { return name; }
        }

        public abstract string SourceFileExtension { get; }
        public abstract string GetInitialSourceText(string ownerName, string realmName);
        public abstract RealmEditingOutcome CompileRealm(string realmName, string sourceFile, string outputFile);

        public Realm LoadRealm(string name, string sourceFile, string storyFile, Player owner)
        {
            return new Realm(server, this, name, sourceFile, storyFile, owner);
        }

        public Instance LoadInstance(Realm realm, string name)
        {
            FileStream stream = new FileStream(realm.StoryFile, FileMode.Open, FileAccess.Read);
            return new Instance(server, realm, stream, name);
        }

        protected static string MakeUUID(string realmName)
        {
            // hash the realm name to produce a "unique" identifier for this realm
            byte[] bytes = Encoding.UTF8.GetBytes(realmName);

            // we need 16 bytes (4-2-2-2-6), SHA-1 gives us 20
            SHA1 sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(bytes);

            StringBuilder sb = new StringBuilder(36);
            for (int i = 0; i < 16; i++)
            {
                if (i == 4 || i == 6 || i == 8 || i == 10)
                    sb.Append('-');

                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Executes a program, waits a finite length of time for it to finish,
        /// and returns what it wrote to standard output.
        /// </summary>
        /// <param name="exeName">The path to the program.</param>
        /// <param name="args">The list of arguments.</param>
        /// <returns>A string containing the program's output, or
        /// <b>null</b> if the program timed out.</returns>
        protected static string Execute(string exeName, params string[] args)
        {
            StringBuilder argText = new StringBuilder();
            foreach (string arg in args)
            {
                if (argText.Length > 0)
                    argText.Append(' ');

                if (arg.Contains(" "))
                {
                    argText.Append('"');
                    argText.Append(arg);
                    argText.Append('"');
                }
                else
                    argText.Append(arg);
            }

            using (Process proc = new Process())
            {
                proc.StartInfo.FileName = exeName;
                proc.StartInfo.Arguments = argText.ToString();
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;

                /*Console.WriteLine("Executing: \"{0}\" {1}",
                    proc.StartInfo.FileName,
                    proc.StartInfo.Arguments);*/

                StringBuilder output = new StringBuilder();
                proc.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (output.Length > 0)
                        output.AppendLine();
                    output.Append(e.Data);
                };

                proc.Start();
                proc.BeginOutputReadLine();

                bool exited = proc.WaitForExit(Properties.Settings.Default.CompilerTimeout);
                if (exited)
                {
                    return output.ToString();
                }
                else
                {
                    try
                    {
                        proc.Kill();
                    }
                    catch { /* ignore */ }
                    return null;
                }
            }
        }

        public static void CopyDirectory(string sourceDirectory, string targetDirectory)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo diTarget = new DirectoryInfo(targetDirectory);

            CopyDirectory(diSource, diTarget);
        }

        protected static void CopyDirectory(DirectoryInfo source, DirectoryInfo target)
        {
            // Check if the target directory exists, if not, create it.
            if (Directory.Exists(target.FullName) == false)
            {
                Directory.CreateDirectory(target.FullName);
            }

            // Copy each file into its new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                fi.CopyTo(Path.Combine(target.ToString(), fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                if (diSourceSubDir.Name.ToLower() == ".svn")
                    continue;

                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyDirectory(diSourceSubDir, nextTargetSubDir);
            }
        }
    }

    class InformRealmFactory : RealmFactory
    {
        private readonly string niCompilerPath, niExtensionDir;
        private readonly string infCompilerPath, infLibraryDir;

        public InformRealmFactory(Server server, string name,
            string niCompilerPath, string niExtensionDir,
            string infCompilerPath, string infLibraryDir)
            : base(server, name)
        {
            this.niCompilerPath = niCompilerPath;
            this.niExtensionDir = niExtensionDir;
            this.infCompilerPath = infCompilerPath;
            this.infLibraryDir = infLibraryDir;
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

            if (!Directory.Exists(server.IndexPath))
                Directory.CreateDirectory(server.IndexPath);

            // copy Index
            string realmIndexPath = Path.Combine(server.IndexPath, realmName);

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

                server.LogMessage(LogLevel.Warning, "NI hung while compiling '{0}'", realmName);

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

                        server.LogMessage(LogLevel.Warning, "Inform 6 hung while compiling '{0}'", realmName);

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

        private static readonly string[] niBins = { "ni", "ni.exe" };
        private static readonly string[] i6Bins = { "inform-6.31-biplatform", "inform-631.exe" };

        public static bool FindCompilers(string dir, out string nibin, out string i6bin)
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
    }
}
