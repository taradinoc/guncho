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
    [Route("api/players")]
    [Authorize]
    public sealed class PlayersController : GunchoApiController
    {
        private readonly IPlayerService _playerService;
        private readonly Microsoft.Extensions.Logging.ILogger<PlayersController> _logger;

        public PlayersController(IPlayerService playerService, Microsoft.Extensions.Logging.ILogger<PlayersController> logger)
        {
            _playerService = playerService;
            _logger = logger;
        }

        private PlayerDto MakeDto(Player p)
        {
            return new PlayerDto
            {
                Id = p.ID,
                Name = p.Name,
                IsAdmin = p.IsAdmin,
                IsGuest = p.IsGuest,
                Attributes = p.GetAllAttributes().ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        [HttpGet]
        public IEnumerable<PlayerDto> Get()
        {
            return _playerService.GetAllPlayers().Select(p => MakeDto(p));
        }

        [HttpGet("{name}")]
        public async Task<IActionResult> GetPlayerByNameAsync(string name)
        {
            _logger.LogDebug("PlayersController.GetPlayerByNameAsync called with name: {0}", name);
            
            // Handle "me" as a special case
            if (name.Equals("me", StringComparison.OrdinalIgnoreCase))
            {
                var userName = User?.Identity?.Name;
                _logger.LogDebug("Handling 'me' request, userName from identity: {0}", userName ?? "(null)");
                
                if (string.IsNullOrEmpty(userName))
                {
                    _logger.LogWarning("'me' request but no userName in identity");
                    return Unauthorized();
                }

                var currentPlayer = await _playerService.GetPlayerByNameAsync(userName);
                if (currentPlayer == null)
                {
                    _logger.LogError("Player not found for userName: {0}", userName);
                    return NotFound();
                }

                _logger.LogDebug("Returning player: {0}", currentPlayer.Name);
                return Ok(MakeDto(currentPlayer));
            }

            _logger.LogDebug("Looking up player by name: {0}", name);
            var player = await _playerService.GetPlayerByNameAsync(name);

            if (player == null)
            {
                return NotFound();
            }

            return Ok(MakeDto(player));
        }
    
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPlayerByIdAsync(string id, [FromBody] PlayerDto updatedPlayer)
        {
            _logger.LogDebug("PlayersController.PutPlayerByIdAsync called with id: {0}", id);
            
            var player = await _playerService.GetPlayerByIdAsync(id);
            if (player == null)
            {
                _logger.LogWarning("Player not found for id: {0}", id);
                return NotFound();
            }

            // Check if user is authorized to edit this player
            var userName = User?.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
            {
                return Unauthorized();
            }

            var currentUser = await _playerService.GetPlayerByNameAsync(userName);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Users can only edit their own profile (unless admin)
            if (player.ID != currentUser.ID && !currentUser.IsAdmin)
            {
                return Forbid();
            }

            // Update writable attributes
            if (updatedPlayer.Attributes != null)
            {
                var writableAttributes = new[] { "description", "gender", "pronouns_1p", "pronouns_2p", "pronouns_3p" };
                
                foreach (var pair in updatedPlayer.Attributes)
                {
                    if (writableAttributes.Contains(pair.Key))
                    {
                        player.SetAttribute(pair.Key, pair.Value);
                    }
                }
            }

            await _playerService.SavePlayerAsync(player);
            
            _logger.LogDebug("Player {0} updated successfully", player.Name);
            return Ok(MakeDto(player));
        }
    }
}