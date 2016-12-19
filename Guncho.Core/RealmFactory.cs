using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Web;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Guncho
{
    public abstract class RealmFactory
    {
        private readonly string name;

        protected RealmFactory(string factoryName)
        {
            this.name = factoryName;
        }

        public string Name
        {
            get { return name; }
        }

        public abstract string SourceFileExtension { get; }
        public abstract string GetInitialSourceText(string ownerName, string realmName);
        public abstract Task<RealmEditingOutcome> CompileRealmAsync(string realmName, string sourceFile, string outputFile);

        public Realm LoadRealm(Server server, string name, string sourceFile, string storyFile, Player owner)
        {
            return new Realm(this, name, sourceFile, storyFile, owner);
        }

        public abstract IInstance LoadInstance(IInstanceSite site, Realm realm, string name, ILogger logger);

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
        protected static /*async*/ Task<string> ExecuteAsync(string exeName, params string[] args)
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
                //proc.EnableRaisingEvents = true;
                proc.StartInfo.FileName = exeName;
                proc.StartInfo.Arguments = argText.ToString();
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;

                /*Console.WriteLine("Executing: \"{0}\" {1}",
                    proc.StartInfo.FileName,
                    proc.StartInfo.Arguments);*/

                StringBuilder output = new StringBuilder();
                proc.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    if (output.Length > 0)
                        output.AppendLine();
                    output.Append(e.Data);
                };

                //var tcs = new TaskCompletionSource();

                //proc.Exited += delegate (object sender, EventArgs e)
                //{
                //    tcs.SetResult();
                //};

                proc.Start();
                proc.BeginOutputReadLine();

                if (proc.WaitForExit(Properties.Settings.Default.CompilerTimeout))
                {
                    return Task.FromResult(output.ToString());
                }
                else
                {
                    try { proc.Kill(); }
                    catch { }
                    return Task.FromResult<string>(null);
                }

                //var timedOut = Task.Delay(Properties.Settings.Default.CompilerTimeout);

                //if (await Task.WhenAny(tcs.Task, timedOut) != timedOut)
                //{
                //    // exited normally
                //    return output.ToString();
                //}
                //else
                //{
                //    // timed out
                //    try
                //    {
                //        proc.Kill();
                //    }
                //    catch
                //    {
                //        // ignore
                //    }
                //    return null;
                //}
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
}
