using Owin;
using System.Web.Http;
using System.Web.Http.Dependencies;

namespace Guncho
{
    public class WebApiStartup
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

            config.EnableSystemDiagnosticsTracing();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            appBuilder.UseWebApi(config);
        }
    }
}