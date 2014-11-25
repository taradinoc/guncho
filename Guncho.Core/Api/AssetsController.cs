using Guncho.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Guncho.Api
{
    public class AssetRefDto
    {
        public string Path;
        public string Uri;
        public string ContentType;
    }

    [RoutePrefix("api/assets")]
    public sealed class AssetsController : ApiController
    {
        private readonly IRealmsService realmsService;

        public AssetsController(IRealmsService realmsService)
        {
            this.realmsService = realmsService;
        }

        [Route("realm/{realmName}")]
        public IHttpActionResult GetRealmAssetManifest(string realmName)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            return Ok(new[] {
                new AssetRefDto {
                    Path = "/story.ni",
                    Uri = Url.Link("GetAssetByPath", new { realmName = realmName, path = "story.ni" }),
                    ContentType = ContentTypes.Inform7Source,
                }
            });
        }

        [Route("realm/{realmName}/{*path}", Name = "GetAssetByPath")]
        public IHttpActionResult GetByPath(string realmName, string path)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            // TODO: return other assets
            if (path == "story.ni")
            {
                return new FileResult(realm.SourceFile, contentType: ContentTypes.Inform7Source);
            }

            return NotFound();
        }
    }
}
