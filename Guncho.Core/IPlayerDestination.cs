using System.Collections.Generic;
using System.Threading.Tasks;

namespace Guncho
{
    public interface IPlayerDestination<TSavedPosition>
    {
        Task AddPlayerAsync(Player player, TSavedPosition position);
        Task RemovePlayerAsync(Player player);
        Player[] ListPlayers();
        Task ExportPlayerPositionsAsync(IDictionary<Player, TSavedPosition> results);
    }
}