using Microsoft.AspNet.Identity;
using Microsoft.Owin;
using Microsoft.Owin.Security.OAuth;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Owin;
using System;
using System.Web.Http;
using System.Web.Http.Dependencies;
using Thinktecture.IdentityModel.Owin.ResourceAuthorization;

namespace Guncho.Api
{
    public sealed class WebApiStartup
    {
        private readonly IDependencyResolver resolver;
        private readonly IResourceAuthorizationManager resourceAuth;
        private readonly IOAuthAuthorizationServerProvider oauthServerProvider;

        public WebApiStartup(IDependencyResolver resolver,
            IResourceAuthorizationManager resourceAuth,
            IOAuthAuthorizationServerProvider oauthServerProvider)
        {
            this.resolver = resolver;
            this.resourceAuth = resourceAuth;
            this.oauthServerProvider = oauthServerProvider;
        }

        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            HttpConfiguration config = new HttpConfiguration();
            config.DependencyResolver = resolver;
            config.Filters.Add(new AuthorizeAttribute());
            config.MessageHandlers.Add(new HeadHandler());

            // Configure SignalR.
            appBuilder.MapSignalR();

            // Configure authorization.
            ConfigureOAuth(appBuilder);
            appBuilder.UseResourceAuthorization(resourceAuth);
            appBuilder.UseCors(Microsoft.Owin.Cors.CorsOptions.AllowAll);

            // Configure JSON formatting.
            ConfigureJson(config);

            // Configure routing.
            config.MapHttpAttributeRoutes();

            // Configure tracing.
            config.EnableSystemDiagnosticsTracing();

            // Ready to go.
            config.EnsureInitialized();
            appBuilder.UseWebApi(config);
        }

        private void ConfigureOAuth(IAppBuilder app)
        {
            OAuthAuthorizationServerOptions OAuthServerOptions = new OAuthAuthorizationServerOptions()
            {
                AllowInsecureHttp = true,
                TokenEndpointPath = new PathString("/api/token"),
                AccessTokenExpireTimeSpan = TimeSpan.FromDays(1),
                Provider = oauthServerProvider,
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
        }
    }
}