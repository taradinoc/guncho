using Microsoft.AspNet.Identity;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Microsoft.Owin.Security.OAuth;
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

        public WebApiStartup(
            IWebDependencyResolver webResolver,
            ISignalRDependencyResolver sigrResolver,
            IResourceAuthorizationManager resourceAuth,
            IOAuthAuthorizationServerProvider oauthServerProvider)
        {
            this.webResolver = webResolver;
            this.sigrResolver = sigrResolver;
            this.resourceAuth = resourceAuth;
            this.oauthServerProvider = oauthServerProvider;
        }

        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure authorization.
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

            // Map Web API.
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
            jsonSettings.Converters.Add(new StringEnumConverter { CamelCaseText = true });
        }
    }
}