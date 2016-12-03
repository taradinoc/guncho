using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Guncho.Services
{
    [ContractClass(typeof(IPlayersServiceContract))]
    public interface IPlayersService
    {
        IEnumerable<Player> GetAllPlayers();
        Player GetPlayerByName(string name);
        Player GetPlayerById(int id);

        bool IsValidNameChange(string oldName, string newName);

        Task<bool> TransactionalUpdateAsync(Player player, Func<Player, bool> transaction);
    }

    public static class PlayersServiceConstants
    {
        public const string UserNameRegexPattern = @"(?i)^(?!guest)[a-z][-a-z0-9_]*$";
        public static readonly Regex UserNameRegex = new Regex(UserNameRegexPattern);
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

        public bool IsValidNameChange(string oldName, string newName)
        {
            Contract.Requires(oldName != null);
            Contract.Requires(newName != null);
            return default(bool);
        }

        public Task<bool> TransactionalUpdateAsync(Player player, Func<Player, bool> transaction)
        {
            Contract.Requires(player != null);
            Contract.Requires(transaction != null);
            return default(Task<bool>);
        }
    }
}
