using Microsoft.AspNet.SignalR;
using SimpleInjector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho.Api
{
    // https://stackoverflow.com/questions/10555791/using-simpleinjector-with-signalr
    public class SimpleInjectorSignalRDependencyResolver : DefaultDependencyResolver
    {
        private readonly Container _container;

        public SimpleInjectorSignalRDependencyResolver(Container container)
        {
            _container = container;
        }
        public override object GetService(Type serviceType)
        {
            return ((IServiceProvider)_container).GetService(serviceType)
                   ?? base.GetService(serviceType);
        }

        public override IEnumerable<object> GetServices(Type serviceType)
        {
            return _container.GetAllInstances(serviceType)
                .Concat(base.GetServices(serviceType));
        }
    }
}
