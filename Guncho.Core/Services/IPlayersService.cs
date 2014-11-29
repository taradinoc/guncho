using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho.Services
{
    [ContractClass(typeof(IPlayersServiceContract))]
    public interface IPlayersService
    {
        IEnumerable<Player> GetAllPlayers();
        Player GetPlayerByName(string name);
        Player GetPlayerById(int id);
    }

    [ContractClassFor(typeof(IPlayersService))]
    abstract class IPlayersServiceContract : IPlayersService
    {
        public IEnumerable<Player> GetAllPlayers()
        {
            Contract.Ensures(Contract.Result<IEnumerable<Player>>() != null);
            return default(IEnumerable<Player>);
        }

        public Player GetPlayerByName(string name)
        {
            Contract.Requires(name != null);
            return default(Player);
        }

        public abstract Player GetPlayerById(int id);
    }
}
