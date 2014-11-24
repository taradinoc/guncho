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

            var jsonSettings = config.Formatters.JsonFormatter.SerializerSettings;
            //jsonSettings.Formatting = Formatting.Indented;
            jsonSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();

            config.EnableSystemDiagnosticsTracing();

            config.MapHttpAttributeRoutes();

            config.EnsureInitialized();

            appBuilder.UseWebApi(config);
        }
    }
}