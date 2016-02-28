using Guncho.Api.Security;
using Guncho.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Results;

namespace Guncho.Api.Controllers
{
    public sealed class RealmDto
    {
        [Required]
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
        [Required]
        public string User;
        [Required]
        public RealmAccessLevel Access;
    }

    public sealed class CompilerOptionsDto
    {
        [Required]
        public string Language;
        [Required]
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
        private readonly IPlayersService playersService;
        private readonly ServerConfig config;

        public RealmsController(IRealmsService realmsService, IPlayersService playersService, ServerConfig config)
        {
            this.realmsService = realmsService;
            this.playersService = playersService;
            this.config = config;
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
            }

            if (!Request.CheckAccess(GunchoResources.RealmActions.View, GunchoResources.Realm, realmName))
            {
                return Forbidden();
            }

            return Ok(MakeDto(realm, details: true));
        }

        private class Check<T>
        {
            public Check(Func<T, bool> checkFunc, string modelKey, string errorMsg)
            {
                this.CheckFunc = checkFunc;
                this.ModelKey = modelKey;
                this.ErrorMsg = errorMsg;
            }

            public Func<T, bool> CheckFunc { get; private set; }
            public string ModelKey { get; private set; }
            public string ErrorMsg { get; private set; }
        }

        [Route("{realmName}", Name = "PutRealmByName")]
        public IHttpActionResult PutRealmByName(string realmName, RealmDto newSettings)
        {
            // TODO: use ETags for concurrency control

            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            if (!Request.CheckAccess(GunchoResources.RealmActions.Edit, GunchoResources.Realm, realmName))
            {
                return Forbidden();
            }

            var checks = new Queue<Check<Realm>>();
            var updates = new Queue<Action<Realm>>();
            // TODO: don't modify Realm objects, do everything through service methods (see ProfilesController)

            if (newSettings.Name != null && newSettings.Name != realm.Name)
            {
                checks.Enqueue(new Check<Realm>(
                    r => realmsService.IsValidNameChange(r.Name, newSettings.Name),
                    "Name", "Invalid realm name."));
                checks.Enqueue(new Check<Realm>(
                    r => Request.CheckAccess(
                        GunchoResources.RealmActions.Edit,
                        GunchoResources.Realm, r.Name,
                        GunchoResources.Field, GunchoResources.RealmFields.Name),
                    "Name", "Permission denied."));
                updates.Enqueue(r => r.Name = newSettings.Name);
            }

            if (newSettings.Owner != realm.Owner.Name)
            {
                checks.Enqueue(new Check<Realm>(
                    r => Request.CheckAccess(
                        GunchoResources.RealmActions.Edit,
                        GunchoResources.Realm, r.Name,
                        GunchoResources.Field, GunchoResources.RealmFields.Owner),
                    "Owner", "Permission denied."));
                checks.Enqueue(new Check<Realm>(
                    r => playersService.GetPlayerByName(newSettings.Owner) != null,
                    "Owner", "No such player."));
                updates.Enqueue(r => r.Owner = playersService.GetPlayerByName(newSettings.Owner) ?? r.Owner);
            }

            // acl
            if (newSettings.Acl != null && !AclEqualsDto(realm.AccessList, newSettings.Acl))
            {
                checks.Enqueue(new Check<Realm>(
                    r => Request.CheckAccess(
                        GunchoResources.RealmActions.Edit,
                        GunchoResources.Realm, r.Name,
                        GunchoResources.Field, GunchoResources.RealmFields.Acl),
                    "Acl", "Permission denied."));
                checks.Enqueue(new Check<Realm>(
                    r => newSettings.Acl.All(e => playersService.GetPlayerByName(e.User) != null),
                    "Acl", "No such player(s)."));
                updates.Enqueue(r =>
                {
                    try
                    {
                        r.AccessList = DtoToAcl(newSettings.Acl);
                    }
                    catch (InvalidOperationException)
                    {
                        // don't change ACL if some player names are invalid
                    }
                });
            }

            // privacy
            if (newSettings.Privacy != realm.PrivacyLevel)
            {
                checks.Enqueue(new Check<Realm>(
                    r => Request.CheckAccess(
                        GunchoResources.RealmActions.Edit,
                        GunchoResources.Realm, r.Name,
                        GunchoResources.Field, GunchoResources.RealmFields.Privacy),
                    "Privacy", "Permission denied."));
                updates.Enqueue(r => r.PrivacyLevel = newSettings.Privacy);
            }

            // compiler
            if (!CompilerEqualsFactory(newSettings.Compiler, realm.Factory))
            {
                checks.Enqueue(new Check<Realm>(
                    r => realmsService.GetRealmFactories().Count(f => CompilerEqualsFactory(newSettings.Compiler, f)) == 1,
                    "Compiler", "Invalid compiler settings."));
                updates.Enqueue(r =>
                    r.Factory = realmsService.GetRealmFactories().Single(f => CompilerEqualsFactory(newSettings.Compiler, f)));

                // TODO: recompile realm if compiler setting is changed
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = realmsService.TransactionalUpdate(
                realm,
                r =>
                {
                    bool ok = true;

                    foreach (var check in checks)
                    {
                        if (!check.CheckFunc(r))
                        {
                            ok = false;
                            ModelState.AddModelError(check.ModelKey, check.ErrorMsg);
                        }
                    }

                    if (!ok)
                    {
                        return false;
                    }

                    foreach (var update in updates)
                    {
                        update(r);
                    }

                    return true;
                });

            if (result == false)
            {
                return BadRequest(ModelState);
            }

            return GetRealmByName(realmName);
        }

        private RealmAccessListEntry[] DtoToAcl(IEnumerable<RealmAclEntryDto> dtos)
        {
            var result = new List<RealmAccessListEntry>();
            
            foreach (var dto in dtos)
            {
                var player = playersService.GetPlayerByName(dto.User);
                if (player == null)
                {
                    throw new InvalidOperationException("No such player: " + dto.User);
                }
                result.Add(new RealmAccessListEntry(player, dto.Access));
            }

            return result.ToArray();
        }

        private bool AclEqualsDto(RealmAccessListEntry[] acl, IEnumerable<RealmAclEntryDto> dto)
        {
            var dtoByName = dto.ToDictionary(e => e.User);

            return acl.Length == dtoByName.Count && acl.All(entry =>
            {
                RealmAclEntryDto matchingDto;
                return dtoByName.TryGetValue(entry.Player.Name, out matchingDto) && matchingDto.Access == entry.Level;
            });
        }

        private bool CompilerEqualsFactory(CompilerOptionsDto dto, RealmFactory factory)
        {
            // TODO: support factory languages other than Inform 7
            return dto.Language == "Inform 7" && factory.Name == dto.Version;
        }

        [Route("", Name = "PostNewRealm")]
        public IHttpActionResult PostNewRealm(RealmDto newRealm)
        {
            // verify permission
            if (!Request.CheckAccess(GunchoResources.RealmActions.Create, GunchoResources.Realm, newRealm.Name))
            {
                return Forbidden();
            }

            // fill in defaults
            if (newRealm.Compiler == null)
            {
                newRealm.Compiler = new CompilerOptionsDto()
                {
                    Language = config.DefaultCompilerLanguage,
                    Version = config.DefaultCompilerVersion,
                };
            }

            newRealm.Owner = User.Identity.Name;

            // validate request
            var factory = realmsService.GetRealmFactories().SingleOrDefault(f => CompilerEqualsFactory(newRealm.Compiler, f));

            if (factory == null)
            {
                ModelState.AddModelError("Compiler", "Invalid compiler settings.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (realmsService.GetRealmByName(newRealm.Name) != null)
            {
                return Conflict();
            }

            // create the realm
            var realm = realmsService.CreateRealm(playersService.GetPlayerByName(User.Identity.Name), newRealm.Name, factory);

            if (realm == null)
            {
                // that's weird!
                if (realmsService.GetRealmByName(newRealm.Name) != null)
                {
                    return Conflict();
                }
                else
                {
                    return BadRequest(ModelState);
                }
            }

            // invoke the PUT handler to update any other settings
            var innerResult = PutRealmByName(newRealm.Name, newRealm) as OkNegotiatedContentResult<RealmDto>;

            if (innerResult != null)
            {
                // TODO: if PUT failed, delete the realm and return an error
                return Created(Url.Link("GetRealmByName", new { realmName = newRealm.Name }), innerResult.Content);
            }

            innerResult = GetRealmByName(newRealm.Name) as OkNegotiatedContentResult<RealmDto>;
            return Created(Url.Link("GetRealmByName", new { realmName = newRealm.Name }), GetRealmByName(newRealm.Name));
        }

        [Route("compilers")]
        [AllowAnonymous]
        public IEnumerable<CompilerOptionsDto> GetCompilers()
        {
            return realmsService.GetRealmFactories().Select(f => MakeDto(f, details: true));
        }
    }
}
