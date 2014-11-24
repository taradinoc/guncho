using Guncho.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Guncho.Api
{
    public class RealmDto
    {
        public string Name;
        public string Owner;
        public RealmFactoryDto Type;
    }

    public class RealmFactoryDto
    {
        public string Version;
    }

    [RoutePrefix("api/realms")]
    public class RealmsController : ApiController
    {
        private readonly IRealmsService realmsService;

        public RealmsController(IRealmsService realmsService)
        {
            this.realmsService = realmsService;
        }

        [Route("")]
        public IEnumerable<RealmDto> Get()
        {
            return realmsService.GetAllRealms().Select(
                r => new RealmDto
                {
                    Name = r.Name,
                    Owner = r.Owner.Name,
                    Type = new RealmFactoryDto
                    {
                        Version = r.Factory.Name,
                    },
                });
        }
    }
}
