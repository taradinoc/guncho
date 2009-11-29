using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.IO;

namespace Guncho
{
    public partial class GunchoService : ServiceBase
    {
        private Thread serverThread;
        private Server svr;
        private readonly object serverThreadLock = new object();

        public GunchoService()
        {
            InitializeComponent();
        }

        private void ServerThreadProc(object arg)
        {
            Thread.CurrentThread.Name = "Server Net";

            string homeDir = Properties.Settings.Default.CachePath;
            Environment.SetEnvironmentVariable("HOME", homeDir);

            Directory.CreateDirectory(homeDir + @"\Inform\Documentation");
            Directory.CreateDirectory(homeDir + @"\Inform\Extensions");

            using (FileLogger logger = new FileLogger(
                Properties.Settings.Default.LogPath,
                Properties.Settings.Default.LogSpam))
            {
                svr = new Server(Properties.Settings.Default.GameServerPort, logger);
                svr.Run();
                svr.LogMessage(LogLevel.Notice, "Service terminating.");
            }

            lock (serverThreadLock)
                serverThread = null;

            this.Stop();
        }

        protected override void OnStart(string[] args)
        {
            serverThread = new Thread(ServerThreadProc);
            serverThread.Start();
        }

        protected override void OnStop()
        {
            Thread oldThread;

            lock (serverThreadLock)
            {
                oldThread = serverThread;
                serverThread = null;
            }

            if (oldThread != null)
            {
                svr.Shutdown("Stopping the service");

                if (oldThread != System.Threading.Thread.CurrentThread)
                {
                    RequestAdditionalTime(Properties.Settings.Default.EventGranularity * 2);
                    oldThread.Join();
                }
            }
        }

        protected override void OnShutdown()
        {
            if (serverThread != null)
            {
                svr.Shutdown("Host is shutting down");

                RequestAdditionalTime(Properties.Settings.Default.EventGranularity * 2);

                Thread oldThread = serverThread;
                serverThread = null;
                oldThread.Join();
            }
        }
    }
}
