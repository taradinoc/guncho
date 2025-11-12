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
    public class ProfileDto
    {
        public required string Name { get; set; }
        public required string Uri { get; set; }
        public required IDictionary<string, string> Attributes { get; set; }
    }

    [Route("api/profiles")]
    [Authorize]
    public sealed class ProfilesController : GunchoApiController
    {
        private static readonly string[] writableAttributes =
        {
            // A human-readable description, shown when examining the player.
            "description",

            // A human-readable gender, e.g. "male", "female", "other"
            "gender",
            // 1st person pronouns, e.g. "s=I|o=me|p=my|ps=mine|r=myself|v=am"
            "pronouns_1p",
            // 2nd person pronouns, e.g. "s=you|o=you|p=your|ps=yours|r=yourself|v=are"
            "pronouns_2p",
            // 3rd person pronouns, e.g. "s=he|o=him|p=his|ps=his|r=himself|v=is"
            "pronouns_3p",
        };

        private readonly IPlayerService _playerService;

        public ProfilesController(IPlayerService playerService)
        {
            _playerService = playerService;
        }

        private ProfileDto MakeDto(Player p)
        {
            var result = new ProfileDto
            {
                Name = p.Name,
                Uri = Url.Link("GetProfileByName", new { name = p.Name }) ?? string.Empty,
                Attributes = new Dictionary<string, string>(),
            };

            // TODO: Implement authorization checks
            foreach (var pair in p.GetAllAttributes())
            {
                result.Attributes.Add(pair);
            }

            return result;
        }

        [HttpGet("")]
        public IEnumerable<ProfileDto> Get()
        {
            return _playerService.GetAllPlayers().Select(p => MakeDto(p));
        }

        [HttpGet("{name}", Name = "GetProfileByName")]
        public async Task<IActionResult> GetProfileByNameAsync(string name)
        {
            var player = await _playerService.GetPlayerByNameAsync(name);

            if (player == null)
            {
                return NotFound();
            }

            return Ok(MakeDto(player));
        }

        [HttpPut("{name}")]
        public async Task<IActionResult> PutProfileByNameAsync(string name, [FromBody] ProfileDto newProfile)
        {
            var player = await _playerService.GetPlayerByNameAsync(name);

            if (player == null)
            {
                return NotFound();
            }

            // TODO: Implement authorization check
            if (User?.Identity?.Name != player.Name)
            {
                return Forbidden();
            }

            // Handle name changes
            if (newProfile.Name != null && newProfile.Name != player.Name)
            {
                // TODO: Validate name change rules
                player.Name = newProfile.Name;
            }

            // Handle attribute updates
            if (newProfile.Attributes != null)
            {
                foreach (var pair in newProfile.Attributes)
                {
                    if (!writableAttributes.Contains(pair.Key))
                    {
                        ModelState.AddModelError("Attributes", $"Attribute {pair.Key} is not writable.");
                        continue;
                    }

                    player.SetAttribute(pair.Key, pair.Value);
                }
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _playerService.SavePlayerAsync(player);

            return Ok(MakeDto(player));
        }

        [HttpDelete("{name}")]
        public async Task<IActionResult> DeleteProfileByNameAsync(string name)
        {
            var player = await _playerService.GetPlayerByNameAsync(name);

            if (player == null)
            {
                return NotFound();
            }

            // TODO: Implement authorization check
            if (User?.Identity?.Name != player.Name && !(User?.IsInRole("Admin") ?? false))
            {
                return Forbidden();
            }

            await _playerService.DeletePlayerAsync(player);

            return NoContent();
        }
    }
}
