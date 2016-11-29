using System.Collections.Generic;
using System.Threading.Tasks;

namespace Guncho
{
    public interface IPlayerDestination<TSavedPosition>
    {
        Task AddPlayer(Player player, TSavedPosition position);
        Task RemovePlayer(Player player);
        Player[] ListPlayers();
        Task ExportPlayerPositions(IDictionary<Player, TSavedPosition> results);
    }
}