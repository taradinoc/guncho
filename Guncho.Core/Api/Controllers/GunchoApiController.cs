using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Guncho.Api.Controllers
{
    public abstract class GunchoApiController : ApiController
    {
        protected IHttpActionResult File(HttpRequestMessage request, string path, string contentType = null)
        {
            return new FileResult(request, path, contentType);
        }

        protected IHttpActionResult Forbidden()
        {
            return new ForbiddenResult();
        }

        protected IHttpActionResult NoContent()
        {
            return new NoContentResult();
        }

        protected IHttpActionResult UnprocessableEntity()
        {
            return new UnprocessableEntityResult();
        }

        protected IHttpActionResult UnsupportedMediaType()
        {
            return new UnsupportedMediaTypeResult();
        }
    }
}
