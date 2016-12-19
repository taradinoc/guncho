using System;
using System.Collections.Generic;
using System.Linq;
using Guncho.Services;
using System.Threading.Tasks;

namespace Guncho.Api.Tests
{
    internal class FakePlayersService : IPlayersService
    {
        private readonly List<Player> players = new List<Player>();

        public IEnumerable<Player> GetAllPlayers()
        {
            return players;
        }

        public Player GetPlayerById(int id)
        {
            return players.SingleOrDefault(p => p.ID == id); 
        }

        public void Add(string name, bool isAdmin = false, bool isGuest = false)
        {
            var id = players.Count + 1;
            players.Add(new Player(id, name, isAdmin, isGuest));
        }

        public Player GetPlayerByName(string name)
        {
            return players.SingleOrDefault(p => p.Name == name);
        }

        public bool IsValidNameChange(string oldName, string newName)
        {
            return false;
        }

        public Task<bool> TransactionalUpdateAsync(Player player, Func<Player, bool> transaction)
        {
            return Task.FromResult(transaction(player));
        }
    }
}