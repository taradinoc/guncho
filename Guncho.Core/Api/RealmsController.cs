﻿using Guncho.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Guncho.Api
{
    public sealed class RealmDto
    {
        public string Name;
        public string Owner;
        public string Uri;
        public CompilerOptionsDto Compiler;
        public RuntimeOptionsDto Runtime;
        public string Assets;
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
    public sealed class RealmsController : ApiController
    {
        private readonly IRealmsService realmsService;

        public RealmsController(IRealmsService realmsService)
        {
            this.realmsService = realmsService;
        }

        private RealmDto MakeDto(Realm r)
        {
            return new RealmDto
            {
                Name = r.Name,
                Owner = r.Owner.Name,
                Uri = Url.Link("GetRealmByName", new { realmName = r.Name }),
                Compiler = MakeDto(r.Factory),
                Runtime = new RuntimeOptionsDto
                {
                    Platform = "Glulx",
                },
                Assets = Url.Link("GetRealmAssetManifest", new { realmName = r.Name }),
            };
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
            return realmsService.GetAllRealms().Select(r => MakeDto(r));
        }

        [Route("{realmName}", Name = "GetRealmByName")]
        public RealmDto GetRealmByName(string realmName)
        {
            var realm = realmsService.GetRealmByName(realmName);

            if (realm == null)
            {
                throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
            }

            return MakeDto(realm);
        }

        [Route("compilers")]
        public IEnumerable<CompilerOptionsDto> GetCompilers()
        {
            return realmsService.GetRealmFactories().Select(f => MakeDto(f, details: true));
        }
    }
}
