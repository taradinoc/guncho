using Guncho.Api.Security;
using Microsoft.Owin;
using MyTested.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Thinktecture.IdentityModel.Owin.ResourceAuthorization;

namespace Guncho.Api.Tests
{
    static class MyWebApiExtensions
    {
        public static IControllerBuilder<T> WithTestRig<T>(this IControllerBuilder<T> obj, TestRig rig)
            where T : ApiController
        {
            return obj
                .WithResolvedDependencies(rig.PlayersService)
                .WithSetup(c =>
                {
                    var oc = new OwinContext();
                    var auth = new GunchoResourceAuthorization(rig.PlayersService, rig.RealmsService);
                    oc.Set(ResourceAuthorizationManagerMiddleware.Key, auth);
                    c.Request.SetOwinContext(oc);
                });
        }
    }
}
