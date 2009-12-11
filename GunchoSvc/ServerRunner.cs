using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Guncho
{
    public class ServerRunner
    {
        private string homeDir = Properties.Settings.Default.CachePath;
        private int port = Properties.Settings.Default.GameServerPort;
        private ILogger logger;
        private Server svr;

        public string HomeDir
        {
            get { return homeDir; }
            set { homeDir = value; }
        }

        public ILogger Logger
        {
            get { return logger; }
            set { logger = value; }
        }

        public int Port
        {
            get { return port; }
            set { port = value; }
        }

        internal Server Server
        {
            get { return svr; }
        }

        public void Run()
        {
            Environment.SetEnvironmentVariable("HOME", homeDir);

            Directory.CreateDirectory(homeDir + @"\Inform\Documentation");
            Directory.CreateDirectory(homeDir + @"\Inform\Extensions");

            bool ownLogger = false;
            if (logger == null)
            {
                logger = new FileLogger(
                    Properties.Settings.Default.LogPath,
                    Properties.Settings.Default.LogSpam);
                ownLogger = true;
            }

            try
            {
                svr = new Server(port, logger);
                svr.Run();
                svr.LogMessage(LogLevel.Notice, "Service terminating.");
            }
            finally
            {
                svr = null;
                if (ownLogger)
                {
                    if (logger is IDisposable)
                        ((IDisposable)logger).Dispose();
                    logger = null;
                }
            }
        }
    }
}
