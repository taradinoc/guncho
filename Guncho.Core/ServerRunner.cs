using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using SimpleInjector;
using System.Web.Http.Dependencies;
using SimpleInjector.Integration.WebApi;
using System.Web.Http;
using Guncho.Services;
using Guncho.Api;
using Microsoft.AspNet.Identity;
using Thinktecture.IdentityModel.Owin.ResourceAuthorization;
using Guncho.Api.Security;

using IWebDependencyResolver = System.Web.Http.Dependencies.IDependencyResolver;
using ISignalRDependencyResolver = Microsoft.AspNet.SignalR.IDependencyResolver;
using Guncho.Api.Hubs;
using Guncho.Connections;

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
                CachePath = Properties.Settings.Default.CachePath,
                IndexPath = Path.Combine(Properties.Settings.Default.CachePath, "Index"),
            };

            // register auth classes
            container.Register<IUserStore<ApiUser, int>, OldTimeyUserStore>();
            container.RegisterSingle<IPasswordHasher, OldTimeyPasswordHasher>();
            container.Register<UserManager<ApiUser, int>>();
            container.RegisterInitializer<UserManager<ApiUser, int>>(
                um =>
                {
                    um.PasswordHasher = container.GetInstance<IPasswordHasher>();
                });
            container.RegisterSingle<IResourceAuthorizationManager, GunchoResourceAuthorization>();

            // register server classes
            var serverReg = Lifestyle.Singleton.CreateRegistration<Server>(container);
            container.AddRegistration(typeof(Server), serverReg);
            container.AddRegistration(typeof(IRealmsService), serverReg);
            container.AddRegistration(typeof(IPlayersService), serverReg);
            container.RegisterInitializer<Server>(
                s =>
                {
                    // ugly
                    s.ResourceAuthorizationManager = new GunchoResourceAuthorization(s, s);
                });

            container.RegisterSingle<ServerConfig>(serverConfig);
            container.RegisterSingle<ILogger>(logger);
            container.RegisterSingle<IWebDependencyResolver, SimpleInjectorWebApiDependencyResolver>();
            container.RegisterSingle<ISignalRDependencyResolver, SimpleInjectorSignalRDependencyResolver>();

            // register API controller classes
            var webApiLifestyle = new WebApiRequestLifestyle();
            var controllerTypes = from t in typeof(ServerRunner).Assembly.GetTypes()
                                  where !t.IsAbstract && typeof(ApiController).IsAssignableFrom(t)
                                  select t;
            foreach (var type in controllerTypes)
            {
                container.Register(type, type, webApiLifestyle);
            }

            // register SignalR hub and utility classes
            container.RegisterSingle<ISignalRConnectionManager, SignalRConnectionManager>();
            container.Register<PlayHub>();

            // register realm factory classes
            var informRealmFactories = InformRealmFactory.ConstructAll(
                logger: logger,
                installationsPath: Properties.Settings.Default.NiInstallationsPath,
                indexOutputDir: serverConfig.IndexPath);
            container.RegisterAll<InformRealmFactory>(informRealmFactories);

            var allRealmFactories = new List<RealmFactory>();
            allRealmFactories.AddRange(informRealmFactories);
            container.RegisterAll<RealmFactory>(allRealmFactories);

            container.Verify();

            // run server
            try
            {
                svr = container.GetInstance<Server>();
                svr.Run();
                logger.LogMessage(LogLevel.Notice, "Service terminating.");
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
