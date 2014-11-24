using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using SimpleInjector;
using System.Web.Http.Dependencies;
using SimpleInjector.Integration.WebApi;

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
            var container = new Container();

            // set up server configuration
            Environment.SetEnvironmentVariable("HOME", homeDir);

            string idir = Path.Combine(homeDir, "Inform");
            Directory.CreateDirectory(Path.Combine(idir, "Documentation"));
            Directory.CreateDirectory(Path.Combine(idir, "Extensions"));

            bool ownLogger = false;
            if (logger == null)
            {
                logger = new FileLogger(
                    Properties.Settings.Default.LogPath,
                    Properties.Settings.Default.LogSpam);
                ownLogger = true;
            }

            var serverConfig = new ServerConfig
            {
                Port = port,
            };

            // configure Simple Injector
            container.RegisterSingle<Server>();
            container.RegisterSingle<ServerConfig>(serverConfig);
            container.RegisterSingle<ILogger>(logger);
            container.RegisterSingle<IDependencyResolver, SimpleInjectorWebApiDependencyResolver>();

            var webApiLifestyle = new WebApiRequestLifestyle();
            foreach (var type in Guncho.Server.GetApiControllerTypes())
            {
                container.Register(type, type, webApiLifestyle);
            }

            // run server
            try
            {
                svr = container.GetInstance<Server>();
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

        public void Stop(string reason)
        {
            if (svr != null)
                svr.Shutdown(reason);
        }
    }
}
