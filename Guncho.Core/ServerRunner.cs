using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Web.Http;

using Microsoft.AspNet.Identity;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.DataProtection;
using SimpleInjector;
using SimpleInjector.Integration.WebApi;
using Thinktecture.IdentityModel.Owin.ResourceAuthorization;

using Guncho.Api;
using Guncho.Api.Hubs;
using Guncho.Api.Security;
using Guncho.Connections;
using Guncho.Services;

using ISignalRDependencyResolver = Microsoft.AspNet.SignalR.IDependencyResolver;
using IWebDependencyResolver = System.Web.Http.Dependencies.IDependencyResolver;

namespace Guncho
{
    public class ServerRunner
    {
        public string HomeDir { get; set; } = Properties.Settings.Default.CachePath;
        public ILogger Logger { get; set; }
        public int Port { get; set; } = Properties.Settings.Default.GameServerPort;
        public int WebPort { get; set; } = Properties.Settings.Default.WebServerPort;

        internal Server Server { get; private set; }

        public void Run()
        {
            var container = new Container();

            // set up server configuration
            Environment.SetEnvironmentVariable("HOME", HomeDir);

            string idir = Path.Combine(HomeDir, "Inform");
            Directory.CreateDirectory(Path.Combine(idir, "Documentation"));
            Directory.CreateDirectory(Path.Combine(idir, "Extensions"));

            bool ownLogger = false;
            if (Logger == null)
            {
                Logger = new FileLogger(
                    Properties.Settings.Default.LogPath,
                    Properties.Settings.Default.LogSpam);
                ownLogger = true;
            }

            var serverConfig = new ServerConfig
            {
                GamePort = Port,
                WebPort = WebPort,
                CachePath = Properties.Settings.Default.CachePath,
                IndexPath = Path.Combine(Properties.Settings.Default.CachePath, "Index"),
                DefaultCompilerLanguage = "Inform 7",
                DefaultCompilerVersion = "5Z71",
            };

            // register all of our classes
            RegisterAuthClasses(container);
            RegisterServerClasses(container, serverConfig, Logger);
            RegisterApiControllers(container);
            RegisterSignalRClasses(container);
            RegisterRealmFactories(container, serverConfig, Logger);

            container.Verify();

            // run server
            try
            {
                Server = container.GetInstance<Server>();
                Server.RunAsync().Wait();
                Logger.LogMessage(LogLevel.Notice, "Service terminating.");
            }
            finally
            {
                Server = null;
                if (ownLogger)
                {
                    (Logger as IDisposable)?.Dispose();
                    Logger = null;
                }
            }
        }

        private static void RegisterRealmFactories(Container container, ServerConfig serverConfig, ILogger logger)
        {
            var informRealmFactories = InformRealmFactory.ConstructAll(
                            logger: logger,
                            installationsPath: Properties.Settings.Default.NiInstallationsPath,
                            indexOutputDir: serverConfig.IndexPath);
            container.RegisterCollection<InformRealmFactory>(informRealmFactories);

            var allRealmFactories = new List<RealmFactory>();
            allRealmFactories.AddRange(informRealmFactories);
            container.RegisterCollection<RealmFactory>(allRealmFactories);
        }

        private static void RegisterSignalRClasses(Container container)
        {
            container.RegisterSingleton<ISignalRConnectionManager, SignalRConnectionManager>();
            container.RegisterSingleton<PlayHub>();
        }

        private static void RegisterApiControllers(Container container)
        {
            // TODO: use container.RegisterWebApiControllers()
            var webApiLifestyle = new WebApiRequestLifestyle();
            var controllerTypes = from t in typeof(ServerRunner).Assembly.GetTypes()
                                  where !t.IsAbstract && typeof(ApiController).IsAssignableFrom(t)
                                  select t;
            foreach (var type in controllerTypes)
            {
                container.Register(type, type, webApiLifestyle);
            }
        }

        private static void RegisterServerClasses(Container container, ServerConfig serverConfig, ILogger logger)
        {
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

            container.RegisterSingleton<ServerConfig>(serverConfig);
            container.RegisterSingleton<ILogger>(logger);
            container.RegisterSingleton<IWebDependencyResolver>(new SimpleInjectorWebApiDependencyResolver(container));
            container.RegisterSingleton<ISignalRDependencyResolver, SimpleInjectorSignalRDependencyResolver>();
        }

        private static void RegisterAuthClasses(Container container)
        {
            container.RegisterSingleton<IUserStore<ApiUser, int>, OldTimeyUserStore>();
            container.RegisterSingleton<IPasswordHasher, OldTimeyPasswordHasher>();
            container.RegisterWebApiRequest<UserManager<ApiUser, int>>();
            container.RegisterInitializer<UserManager<ApiUser, int>>(
                um =>
                {
                    um.PasswordHasher = container.GetInstance<IPasswordHasher>();
                });
            container.RegisterSingleton<IResourceAuthorizationManager, GunchoResourceAuthorization>();

            var savedSecret = Properties.Settings.Default.WebAuthSecret;
            byte[] secretBytes;
            if (string.IsNullOrEmpty(savedSecret))
            {
                using (var rng = new RNGCryptoServiceProvider())
                {
                    secretBytes = new byte[32];
                    rng.GetBytes(secretBytes);
                    Properties.Settings.Default.WebAuthSecret = Convert.ToBase64String(secretBytes);
                    Properties.Settings.Default.Save();
                }
            }
            else
            {
                secretBytes = Convert.FromBase64String(Properties.Settings.Default.WebAuthSecret);
            }
            container.RegisterSingleton<IDataProtectionProvider>(new GunchoDataProtectionProvider(secretBytes));
            container.RegisterSingleton<ISecureDataFormat<AuthenticationTicket>>(new GunchoTicketFormat(secretBytes));
        }

        public void Stop(string reason)
        {
            Server?.ShutdownAsync(reason).Wait();
        }
    }
}
