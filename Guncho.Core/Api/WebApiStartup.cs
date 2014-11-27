using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Owin;
using System.Web.Http;
using System.Web.Http.Dependencies;

namespace Guncho.Api
{
    public sealed class WebApiStartup
    {
        private readonly IDependencyResolver resolver;

        public WebApiStartup(IDependencyResolver resolver)
        {
            this.resolver = resolver;
        }

        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            HttpConfiguration config = new HttpConfiguration();
            config.DependencyResolver = resolver;
            config.MessageHandlers.Add(new HeadHandler());

            // Configure JSON formatting.
            var jsonSettings = config.Formatters.JsonFormatter.SerializerSettings;
            jsonSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            //jsonSettings.Formatting = Formatting.Indented;
            jsonSettings.NullValueHandling = NullValueHandling.Ignore;

            // Configure routing.
            config.MapHttpAttributeRoutes();

            // Configure tracing.
            config.EnableSystemDiagnosticsTracing();

            // Ready to go.
            config.EnsureInitialized();
            appBuilder.UseWebApi(config);
        }
    }
}