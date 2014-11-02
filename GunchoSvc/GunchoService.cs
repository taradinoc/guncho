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

namespace Guncho.WinService
{
    public partial class GunchoService : ServiceBase
    {
        private Thread serverThread;
        private ServerRunner runner;
        private readonly object serverThreadLock = new object();

        public GunchoService()
        {
            InitializeComponent();
        }

        private void ServerThreadProc(object arg)
        {
            Thread.CurrentThread.Name = "Server Net";

            runner = new ServerRunner();
            runner.Run();

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
                runner.Stop("Stopping the service");

                if (oldThread != System.Threading.Thread.CurrentThread)
                {
                    RequestAdditionalTime(Guncho.Properties.Settings.Default.EventGranularity * 2);
                    oldThread.Join();
                }
            }
        }

        protected override void OnShutdown()
        {
            if (serverThread != null)
            {
                runner.Stop("Host is shutting down");

                RequestAdditionalTime(Guncho.Properties.Settings.Default.EventGranularity * 2);

                Thread oldThread = serverThread;
                serverThread = null;
                oldThread.Join();
            }
        }
    }
}
