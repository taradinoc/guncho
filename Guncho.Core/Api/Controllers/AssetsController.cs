using Guncho.Api.Security;
using Guncho.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Guncho.Api.Controllers
{
    public class AssetManifestDto
    {
        public int Version;
        public string History;
        public IEnumerable<AssetRefDto> Assets;
    }

    public class AssetRefDto
    {
        public string Path;
        public int Version;
        public string Uri;
        public string ContentType;
        public string History;
    }

    public class HistoryDto
    {
        public int LatestVersion;
        public IDictionary<int, HistoryEntryDto> Versions;
    }

    public class HistoryEntryDto
    {
        public string Uri;
        public DateTime Created;
        public string Creator;
    }

    [RoutePrefix("api/assets")]
    public sealed class AssetsController : GunchoApiController
    {
        private readonly IRealmsService realmsService;

        public AssetsController(IRealmsService realmsService)
        {
            this.realmsService = realmsService;
        }

        [Route("realm/{realmName}", Name = "GetRealmAssetManifest")]
        public IHttpActionResult GetRealmAssetManifest(string realmName)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            if (!Request.CheckAccess(GunchoResources.RealmActions.ViewAssets, realmName))
            {
                return Forbidden();
            }

            return Ok(new AssetManifestDto
            {
                Version = 1,
                History = Url.Link("GetRealmAssetManifestHistory", new { realmName = realmName }),
                Assets = new[] {
                    new AssetRefDto {
                        Path = "/story.ni",
                        Version = 1,
                        Uri = Url.Link("GetRealmAssetByPath", new { realmName = realmName, path = "story.ni" }),
                        ContentType = ContentTypes.Inform7Source,
                        History = Url.Link("GetRealmAssetHistoryByPath", new { realmName = realmName, path = "story.ni" }),
                    },
                },
            });
        }

        [Route("history/realm/{realmName}", Name = "GetRealmAssetManifestHistory")]
        public IHttpActionResult GetRealmAssetManifestHistory(string realmName)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            if (!Request.CheckAccess(GunchoResources.RealmActions.ViewAssets, realmName))
            {
                return Forbidden();
            }

            return Ok(new HistoryDto
            {
                LatestVersion = 1,
                Versions = new SortedDictionary<int, HistoryEntryDto>
                {
                    {
                        1,
                        new HistoryEntryDto {
                            Uri = Url.Link("GetRealmAssetManifest", new { realmName = realmName }),
                        }
                    }
                },
            });
        }

        [Route("realm/{realmName}/{*path}", Name = "GetRealmAssetByPath")]
        public IHttpActionResult GetRealmAssetByPath(string realmName, string path)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            if (!Request.CheckAccess(GunchoResources.RealmActions.ViewAssets, realmName, path))
            {
                return Forbidden();
            }

            // TODO: return other assets
            if (path == "story.ni")
            {
                return File(Request, realm.SourceFile, contentType: ContentTypes.Inform7Source);
            }

            return NotFound();
        }

        [Route("realm/{realmName}/{*path}", Name = "PutRealmAssetByPath")]
        public async Task<IHttpActionResult> PutRealmAssetByPath(string realmName, string path)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            if (!Request.CheckAccess(GunchoResources.RealmActions.EditAssets, realmName, path))
            {
                return Forbidden();
            }

            // TODO: support other assets
            if (path == "story.ni")
            {
                var contentType = Request.Content.Headers.ContentType;
                if (contentType.MediaType != ContentTypes.Inform7Source)
                {
                    return UnsupportedMediaType();
                }

                // TODO: include the user id in this call
                var bodyStream = await Request.Content.ReadAsStreamAsync();

                var outcome = await realmsService.UpdateRealmSourceAsync(realm, bodyStream);

                switch (outcome)
                {
                    case RealmEditingOutcome.Success:
                        return Ok(new AssetRefDto
                        {
                            Version = 1,
                            Path = path,
                            Uri = Url.Link("GetRealmAssetByPath", new { realmName = realmName, path = path }),
                        });

                    case RealmEditingOutcome.PermissionDenied:
                        return Forbidden();

                    case RealmEditingOutcome.Missing:
                        // shouldn't get here...?
                        return BadRequest();

                    case RealmEditingOutcome.NiError:
                    case RealmEditingOutcome.InfError:
                    case RealmEditingOutcome.VMError:
                        return UnprocessableEntity();
                }
            }

            return NotFound();
        }

        [Route("history/realm/{realmName}/{*path}", Name = "GetRealmAssetHistoryByPath")]
        public IHttpActionResult GetRealmAssetHistoryByPath(string realmName, string path)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            if (!Request.CheckAccess(GunchoResources.RealmActions.ViewAssets, realmName, path))
            {
                return Forbidden();
            }

            // TODO: return other assets
            if (path == "story.ni")
            {
                return Ok(new HistoryDto
                {
                    LatestVersion = 1,
                    Versions = new SortedDictionary<int, HistoryEntryDto>
                    {
                        {
                            1,
                            new HistoryEntryDto {
                                Uri = Url.Link("GetRealmAssetByPath", new { realmName = realmName, path = path }),
                            }
                        }
                    },
                });
            }

            return NotFound();
        }
    }
}
