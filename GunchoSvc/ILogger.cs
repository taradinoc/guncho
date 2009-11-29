using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Guncho
{
    enum LogLevel
    {
        Spam,
        Verbose,
        Notice,
        Warning,
        Error
    }

    interface ILogger
    {
        void LogMessage(LogLevel level, string text);
    }

    class ConsoleLogger : ILogger
    {
        private object myLock = new object();

        public void LogMessage(LogLevel level, string text)
        {
            lock (myLock)
            {
                switch (level)
                {
                    case LogLevel.Spam:
                        Console.Write("[spam]    "); break;
                    case LogLevel.Verbose:
                        Console.Write("[info]    "); break;
                    case LogLevel.Notice:
                        Console.Write("[notice]  "); break;
                    case LogLevel.Warning:
                        Console.Write("[warning] "); break;
                    case LogLevel.Error:
                        Console.Write("[*ERROR*] "); break;
                }

                Console.WriteLine(text);
            }
        }
    }

    class FileLogger : ILogger, IDisposable
    {
        private StreamWriter wtr;
        private bool wantSpam;

        public FileLogger(string dir, bool wantSpam)
        {
            this.wantSpam = wantSpam;

            const string dateFormat = "yyyyMMdd";
            string filename = "log-" + DateTime.Now.ToString(dateFormat) + ".txt";
            wtr = new StreamWriter(Path.Combine(dir, filename), true);
            wtr.AutoFlush = true;
        }

        public void Dispose()
        {
            if (wtr != null)
            {
                wtr.Dispose();
                wtr = null;
            }
        }

        public void LogMessage(LogLevel level, string text)
        {
            lock (wtr)
            {
                if (level != LogLevel.Spam || wantSpam)
                {
                    switch (level)
                    {
                        case LogLevel.Spam:
                            wtr.Write("[spam]    "); break;
                        case LogLevel.Verbose:
                            wtr.Write("[info]    "); break;
                        case LogLevel.Notice:
                            wtr.Write("[notice]  "); break;
                        case LogLevel.Warning:
                            wtr.Write("[warning] "); break;
                        case LogLevel.Error:
                            wtr.Write("[*ERROR*] "); break;
                    }

                    wtr.WriteLine("[{0:yyMMdd.HHmmss}] {1}", DateTime.Now, text);
                }
            }
        }
    }
}
