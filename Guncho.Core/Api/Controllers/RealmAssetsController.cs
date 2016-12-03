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
        public int Version { get; set; }
        public string HistoryUri { get; set; }
        public IEnumerable<AssetRefDto> Assets { get; set; }
    }

    public class AssetRefDto
    {
        public string Path { get; set; }
        public int Version { get; set; }
        public string Uri { get; set; }
        public string ContentType { get; set; }
        public string HistoryUri { get; set; }
    }

    public class HistoryDto
    {
        public int LatestVersion { get; set; }
        public IDictionary<int, HistoryEntryDto> Versions { get; set; }
    }

    public class HistoryEntryDto
    {
        public string Uri { get; set; }
        public DateTime Created { get; set; }
        public string Creator { get; set; }
    }

    [RoutePrefix("api/realms/{realmName}")]
    public sealed class RealmAssetsController : GunchoApiController
    {
        private readonly IRealmsService realmsService;

        public RealmAssetsController(IRealmsService realmsService)
        {
            this.realmsService = realmsService;
        }

        [Route("manifest", Name = "GetRealmAssetManifest")]
        public IHttpActionResult GetRealmAssetManifest(string realmName)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            if (!Request.CheckAccess(GunchoResources.RealmActions.View, GunchoResources.Realm, realmName))
            {
                return Forbidden();
            }

            return Ok(new AssetManifestDto
            {
                Version = 1,
                HistoryUri = Url.Link("GetRealmAssetManifestHistory", new { realmName = realmName }),
                Assets = new[] {
                    new AssetRefDto {
                        Path = "/story.ni",
                        Version = 1,
                        Uri = Url.Link("GetRealmAssetByPath", new { realmName = realmName, path = "story.ni" }),
                        ContentType = ContentTypes.Inform7Source,
                        HistoryUri = Url.Link("GetRealmAssetHistoryByPath", new { realmName = realmName, path = "story.ni" }),
                    },
                },
            });
        }

        [Route("history/manifest", Name = "GetRealmAssetManifestHistory")]
        public IHttpActionResult GetRealmAssetManifestHistory(string realmName)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            if (!Request.CheckAccess(GunchoResources.RealmActions.ViewHistory, GunchoResources.Realm, realmName))
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

        [Route("asset/{*path}", Name = "GetRealmAssetByPath")]
        public IHttpActionResult GetRealmAssetByPath(string realmName, string path)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            if (!Request.CheckAccess(GunchoResources.RealmActions.View, GunchoResources.Realm, realmName, GunchoResources.Asset, path))
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

        [Route("asset/{*path}", Name = "PutRealmAssetByPath")]
        public async Task<IHttpActionResult> PutRealmAssetByPathAsync(string realmName, string path)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            if (!Request.CheckAccess(GunchoResources.RealmActions.Edit, GunchoResources.Realm, realmName, GunchoResources.Asset, path))
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

        [Route("history/asset/{*path}", Name = "GetRealmAssetHistoryByPath")]
        public IHttpActionResult GetRealmAssetHistoryByPath(string realmName, string path)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            if (!Request.CheckAccess(GunchoResources.RealmActions.ViewHistory, GunchoResources.Realm, realmName, GunchoResources.Asset, path))
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
