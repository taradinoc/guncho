using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Guncho
{
    public class FooController : ApiController
    {
        private readonly Server server;

        public FooController(Server server)
        {
            this.server = server;
        }

        public IEnumerable<string> Get()
        {
            return server.ListRealmFactories();
        }

        public string Get(int id)
        {
            return "value" + id;
        }
    }
}
