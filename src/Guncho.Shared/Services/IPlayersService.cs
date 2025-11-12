using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Guncho.Shared.Models;

namespace Guncho.Shared.Services
{
    public interface IPlayersService
    {
        Task<IEnumerable<PlayerSummaryDto>> GetAllPlayersAsync();
        Task<PlayerDto?> GetPlayerByNameAsync(string name);
        Task<PlayerDto?> GetPlayerByIdAsync(int id);
        Task<bool> UpdatePlayerAsync(int id, PlayerDto player);
        Task<bool> IsValidNameChangeAsync(string oldName, string newName);
        Task<PlayerDto> GetOrCreatePlayerAsync(string name);
    }
}
