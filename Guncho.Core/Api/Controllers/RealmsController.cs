using Guncho.Api.Security;
using Guncho.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;

namespace Guncho.Api.Controllers
{
    public sealed class RealmDto
    {
        public string Name;
        public string Owner;
        public string Uri;
        public CompilerOptionsDto Compiler;
        public RuntimeOptionsDto Runtime;
        public string ManifestUri;
        public RealmPrivacyLevel Privacy;
        public IEnumerable<RealmAclEntryDto> Acl;
    }

    public sealed class RealmAclEntryDto
    {
        public string User;
        public RealmAccessLevel Access;
    }

    public sealed class CompilerOptionsDto
    {
        public string Language;
        public string Version;
        public IEnumerable<RuntimeOptionsDto> SupportedRuntimes;
    }

    public sealed class RuntimeOptionsDto
    {
        public string Platform;
    }

    [RoutePrefix("api/realms")]
    public sealed class RealmsController : GunchoApiController
    {
        private readonly IRealmsService realmsService;

        public RealmsController(IRealmsService realmsService)
        {
            this.realmsService = realmsService;
        }

        private RealmDto MakeDto(Realm r, bool details = false)
        {
            var result = new RealmDto
            {
                Name = r.Name,
                Owner = r.Owner.Name,
                Uri = Url.Link("GetRealmByName", new { realmName = r.Name }),
                Compiler = MakeDto(r.Factory),
                Privacy = r.PrivacyLevel,
            };

            if (details)
            {
                result.Runtime = new RuntimeOptionsDto { Platform = "Glulx", };
                result.ManifestUri = Url.Link("GetRealmAssetManifest", new { realmName = r.Name });
                result.Acl = Array.ConvertAll(r.AccessList, e => new RealmAclEntryDto { User = e.Player.Name, Access = e.Level });
            }

            return result;
        }

        private CompilerOptionsDto MakeDto(RealmFactory f, bool details = false)
        {
            if (f is InformRealmFactory)
            {
                return new CompilerOptionsDto
                {
                    Language = "Inform 7",
                    Version = f.Name,
                    SupportedRuntimes = details ? new[] { new RuntimeOptionsDto { Platform = "Glulx" } } : null,
                };
            }

            throw new NotImplementedException("Unexpected RealmFactory type " + f.GetType());
        }

        [Route("")]
        public IEnumerable<RealmDto> Get()
        {
            return from r in realmsService.GetAllRealms()
                   where Request.CheckAccess(GunchoResources.RealmActions.List, GunchoResources.Realm, r.Name)
                   select MakeDto(r, details: false);
        }

        [Route("my")]
        public IEnumerable<RealmDto> GetMy()
        {
            return from r in realmsService.GetAllRealms()
                   where r.Owner.Name == User.Identity.Name
                   where Request.CheckAccess(GunchoResources.RealmActions.List, GunchoResources.Realm, r.Name)
                   select MakeDto(r, details: false);
        }

        [Route("{realmName}", Name = "GetRealmByName")]
        public IHttpActionResult GetRealmByName(string realmName)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
                throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
            }

            if (!Request.CheckAccess(GunchoResources.RealmActions.View, GunchoResources.Realm, realmName))
            {
                return Forbidden();
            }

            return Ok(MakeDto(realm, details: true));
        }

        [Route("compilers")]
        [AllowAnonymous]
        public IEnumerable<CompilerOptionsDto> GetCompilers()
        {
            return realmsService.GetRealmFactories().Select(f => MakeDto(f, details: true));
        }
    }
}
