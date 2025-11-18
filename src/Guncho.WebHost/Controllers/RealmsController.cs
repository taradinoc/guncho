using Guncho.Services;
using Guncho.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Guncho.WebHost.Controllers
{
    [Route("api/realms")]
    [Authorize]
    public sealed class RealmsController : GunchoApiController
    {
        private readonly IRealmService _realmService;
        private readonly IPlayerService _playerService;
        private readonly IServerConfiguration _config;

        public RealmsController(
            IRealmService realmService,
            IPlayerService playerService,
            IServerConfiguration config)
        {
            _realmService = realmService;
            _playerService = playerService;
            _config = config;
        }

        private RealmSummaryDto MakeSummaryDto(Realm r)
        {
            return new RealmSummaryDto
            {
                Name = r.Name,
                OwnerName = r.Owner.Name,
                PrivacyLevel = (Guncho.Shared.Models.RealmPrivacyLevel)r.PrivacyLevel,
                FactoryName = r.Factory.Name
            };
        }

        private RealmDto MakeDto(Realm r)
        {
            return new RealmDto
            {
                Name = r.Name,
                Id = 0,
                OwnerId = r.Owner.ID,
                OwnerName = r.Owner.Name,
                PrivacyLevel = (Guncho.Shared.Models.RealmPrivacyLevel)r.PrivacyLevel,
                FactoryName = r.Factory.Name,
                IsCondemned = r.IsCondemned,
                AccessList = r.AccessList.Select(e => new RealmAccessListEntryDto
                {
                    PlayerId = e.Player.ID,
                    PlayerName = e.Player.Name,
                    Level = (Guncho.Shared.Models.RealmAccessLevel)e.Level
                }).ToList()
            };
        }

        [HttpGet("")]
    public async Task<IEnumerable<RealmSummaryDto>> GetAsync()
        {
            var allRealms = _realmService.GetAllRealms()
                .Where(r => r != null)
                .Select(r => MakeSummaryDto(r));

            return allRealms;
        }

        [HttpGet("my")]
    public async Task<IEnumerable<RealmSummaryDto>> GetMyAsync()
        {
            var userName = User?.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
            {
                return Enumerable.Empty<RealmSummaryDto>();
            }

            var myRealms = _realmService.GetAllRealms()
                .Where(r => r != null && r.Owner.Name == userName)
                .Select(r => MakeSummaryDto(r));

            return myRealms;
        }

        [HttpGet("{realmName}", Name = "GetRealmByName")]
        public async Task<IActionResult> GetRealmByNameAsync(string realmName)
        {
            var realm = await _realmService.GetRealmAsync(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            // TODO: Implement authorization check
            return Ok(MakeDto(realm));
        }

        [HttpPut("{realmName}")]
        public async Task<IActionResult> PutRealmByNameAsync(string realmName, [FromBody] RealmDto newRealm)
        {
            var realm = await _realmService.GetRealmAsync(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            // TODO: Implement authorization check
            if (User?.Identity?.Name != realm.Owner.Name)
            {
                return Forbidden();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Update privacy level
            if ((Guncho.RealmPrivacyLevel)newRealm.PrivacyLevel != realm.PrivacyLevel)
            {
                realm.PrivacyLevel = (Guncho.RealmPrivacyLevel)newRealm.PrivacyLevel;
            }

            // Update ACL
            if (newRealm.AccessList != null)
            {
                var aclEntries = new List<RealmAccessListEntry>();
                foreach (var entry in newRealm.AccessList)
                {
                    var player = await _playerService.GetPlayerByNameAsync(entry.PlayerName);
                    if (player != null)
                    {
                        aclEntries.Add(new RealmAccessListEntry(player, (Guncho.RealmAccessLevel)entry.Level));
                    }
                }
                realm.AccessList = aclEntries.ToArray();
            }

            await _realmService.SaveRealmAsync(realm);

            return Ok(MakeDto(realm));
        }

        [HttpDelete("{realmName}")]
        public async Task<IActionResult> DeleteRealmByNameAsync(string realmName)
        {
            var realm = await _realmService.GetRealmAsync(realmName);

            if (realm == null)
            {
                return NotFound();
            }

            // TODO: Implement authorization check and realm deletion
            if (User?.Identity?.Name != realm.Owner.Name)
            {
                return Forbidden();
            }

            // TODO: Implement realm deletion in service
            return StatusCode(501); // Not Implemented
        }

        [HttpPost("")]
        public async Task<IActionResult> PostAsync([FromBody] CreateRealmDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userName = User?.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
            {
                return Unauthorized();
            }

            var owner = await _playerService.GetPlayerByNameAsync(userName);
            if (owner == null)
            {
                return Unauthorized();
            }

            // Find the factory
            var factory = _realmService.GetRealmFactories().FirstOrDefault(f => f.Name == dto.FactoryName);
            if (factory == null)
            {
                return BadRequest($"Unknown factory: {dto.FactoryName}");
            }

            // Create the realm
            var realm = await _realmService.CreateRealmAsync(owner, dto.Name, factory);
            if (realm == null)
            {
                return BadRequest("Failed to create realm. The name may be invalid or already in use, or you may have reached the maximum number of realms.");
            }

            return CreatedAtRoute("GetRealmByName", new { realmName = realm.Name }, MakeDto(realm));
        }

        [HttpGet("factories")]
        [AllowAnonymous]
        public IActionResult GetFactories()
        {
            var factories = _realmService.GetRealmFactories();
            return Ok(factories.Select(f => new RealmFactoryDto
            {
                Name = f.Name,
                Description = f.Description
            }));
        }
    }
}
