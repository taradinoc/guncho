using Microsoft.AspNet.Identity;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.DataProtection;
using Microsoft.Owin.Security.OAuth;
using Microsoft.Owin.StaticFiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Owin;
using System;
using System.Web.Http;
using System.Web.Http.Dependencies;
using Thinktecture.IdentityModel.Owin.ResourceAuthorization;

using IWebDependencyResolver = System.Web.Http.Dependencies.IDependencyResolver;
using ISignalRDependencyResolver = Microsoft.AspNet.SignalR.IDependencyResolver;

namespace Guncho.Api
{
    public sealed class WebApiStartup
    {
        private readonly IWebDependencyResolver webResolver;
        private readonly ISignalRDependencyResolver sigrResolver;
        private readonly IResourceAuthorizationManager resourceAuth;
        private readonly IOAuthAuthorizationServerProvider oauthServerProvider;
        private readonly IDataProtectionProvider dataProtectionProvider;
        private readonly ISecureDataFormat<AuthenticationTicket> oauthTokenFormat;

        public WebApiStartup(
            IWebDependencyResolver webResolver,
            ISignalRDependencyResolver sigrResolver,
            IResourceAuthorizationManager resourceAuth,
            IOAuthAuthorizationServerProvider oauthServerProvider,
            IDataProtectionProvider dataProtectionProvider,
            ISecureDataFormat<AuthenticationTicket> oauthTokenFormat)
        {
            this.webResolver = webResolver;
            this.sigrResolver = sigrResolver;
            this.resourceAuth = resourceAuth;
            this.oauthServerProvider = oauthServerProvider;
            this.dataProtectionProvider = dataProtectionProvider;
            this.oauthTokenFormat = oauthTokenFormat;
        }

        public void Configuration(IAppBuilder appBuilder)
        {
            // Map static web site, which doesn't need any authorization.
            ConfigureStaticFiles(appBuilder);

            // Configure authorization.
            appBuilder.SetDataProtectionProvider(dataProtectionProvider);
            appBuilder.UseCors(Microsoft.Owin.Cors.CorsOptions.AllowAll);
            ConfigureOAuth(appBuilder);
            appBuilder.UseResourceAuthorization(resourceAuth);

            // Map SignalR.
            GlobalHost.DependencyResolver.Register(typeof(Guncho.Api.Hubs.PlayHub), () => sigrResolver.GetService(typeof(Guncho.Api.Hubs.PlayHub)));
            appBuilder.MapSignalR();

            // Configure Web API for self-host. 
            HttpConfiguration config = new HttpConfiguration();
            config.DependencyResolver = webResolver;
            config.Filters.Add(new System.Web.Http.AuthorizeAttribute());
            config.MessageHandlers.Add(new HeadHandler());

            // Configure JSON formatting.
            ConfigureJson(config);

            // Configure routing.
            config.MapHttpAttributeRoutes();

            // Configure tracing.
            //config.EnableSystemDiagnosticsTracing();
            //appBuilder.SetLoggerFactory(new DiagnosticsLoggerFactory());

            // Map Web API.
            config.EnsureInitialized();
            appBuilder.UseWebApi(config);
        }

        private static void ConfigureStaticFiles(IAppBuilder appBuilder)
        {
            var staticFileSystem = new EmbeddedResourceFileSystem(typeof(Guncho.Site.Site).Assembly, typeof(Guncho.Site.Site).Namespace);
            appBuilder.UseDefaultFiles(new DefaultFilesOptions
            {
                FileSystem = staticFileSystem
            });
            appBuilder.UseStaticFiles(new StaticFileOptions
            {
                FileSystem = staticFileSystem
            });
        }

        private void ConfigureOAuth(IAppBuilder app)
        {
            OAuthAuthorizationServerOptions OAuthServerOptions = new OAuthAuthorizationServerOptions()
            {
                AllowInsecureHttp = true,
                TokenEndpointPath = new PathString("/api/token"),
                AccessTokenExpireTimeSpan = TimeSpan.FromDays(1),
                Provider = oauthServerProvider,
                AccessTokenFormat = oauthTokenFormat,
                RefreshTokenFormat = oauthTokenFormat,
                AuthorizationCodeFormat = oauthTokenFormat,
            };

            app.UseOAuthAuthorizationServer(OAuthServerOptions);
            app.UseOAuthBearerAuthentication(new OAuthBearerAuthenticationOptions());
        }

        private static void ConfigureJson(HttpConfiguration config)
        {
            var jsonSettings = config.Formatters.JsonFormatter.SerializerSettings;
            jsonSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            //jsonSettings.Formatting = Formatting.Indented;
            jsonSettings.NullValueHandling = NullValueHandling.Ignore;
            jsonSettings.Converters.Add(new StringEnumConverter { CamelCaseText = true });
        }
    }
}