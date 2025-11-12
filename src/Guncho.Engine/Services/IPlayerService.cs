using System.Collections.Generic;
using System.Threading.Tasks;

namespace Guncho.Services
{
    /// <summary>
    /// Service for managing players and authentication.
    /// </summary>
    public interface IPlayerService
    {
        Task<Player?> GetPlayerByNameAsync(string name);
        Task<Player?> GetPlayerByIdAsync(string id);
        Task<bool> ValidateLogInAsync(Player player, string password);
        Task SavePlayerAsync(Player player);
        Task DeletePlayerAsync(Player player);
        IEnumerable<Player> GetAllPlayers();
    }
}
