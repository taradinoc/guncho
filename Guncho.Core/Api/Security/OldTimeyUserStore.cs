using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho.Api.Security
{
    class OldTimeyUserStore : IUserStore<ApiUser, int>, IUserPasswordStore<ApiUser, int>,
        IUserRoleStore<ApiUser, int>
    {
        private readonly Server server;

        public OldTimeyUserStore(Server server)
        {
            this.server = server;
        }

        private ApiUser ToApiUser(Player player)
        {
            if (player == null)
            {
                return null;
            }

            return new ApiUser(player.ID)
            {
                UserName = player.Name,
            };
        }

        #region IUserStore<ApiUser,int> Members

        public Task CreateAsync(ApiUser user)
        {
            string pwdSalt = "", pwdHash = "";

            if (user.PasswordHash != null)
            {
                // TODO: support new-timey hashes
                var parts = user.PasswordHash.Split(new[] { ' ' }, 2);
                pwdSalt = parts[0];
                pwdHash = parts[1];
            }

            var player = server.CreatePlayer(user.UserName, pwdSalt, pwdHash);
            return Task.FromResult(player);
        }

        public Task DeleteAsync(ApiUser user)
        {
            throw new NotImplementedException();
        }

        public Task<ApiUser> FindByIdAsync(int userId)
        {
            var player = server.GetPlayerById(userId);
            return Task.FromResult(ToApiUser(player));
        }

        public Task<ApiUser> FindByNameAsync(string userName)
        {
            var player = server.GetPlayerByName(userName);
            return Task.FromResult(ToApiUser(player));
        }

        public Task UpdateAsync(ApiUser user)
        {
            var player = server.GetPlayerById(user.Id);

            if (player != null)
            {
                lock (player)
                {
                    player.Name = user.UserName;
                    
                    var parts = user.PasswordHash.Split(new[] { ' ' }, 2);
                    player.PasswordSalt = parts[0];
                    player.PasswordHash = parts[1];
                }

                server.SavePlayers();
            }

            return Task.FromResult(0);
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            // nada
        }

        #endregion

        #region IUserPasswordStore<ApiUser,int> Members

        public Task<string> GetPasswordHashAsync(ApiUser user)
        {
            var player = server.GetPlayerByName(user.UserName);
            return Task.FromResult(player.PasswordSalt + " " + player.PasswordHash);
        }

        public Task<bool> HasPasswordAsync(ApiUser user)
        {
            return Task.FromResult(true);
        }

        public Task SetPasswordHashAsync(ApiUser user, string passwordHash)
        {
            user.PasswordHash = passwordHash;

            var parts = passwordHash.Split(new[] { ' ' }, 2);

            var player = server.GetPlayerByName(user.UserName);
            if (player != null)
            {
                player.PasswordSalt = parts[0];
                player.PasswordHash = parts[1];

                return Task.Run(() => server.SavePlayers());
            }
            else
            {
                return Task.FromResult(0);
            }
        }

        #endregion

        #region IUserRoleStore<ApiUser,int> Members

        public Task AddToRoleAsync(ApiUser user, string roleName)
        {
            // roles are read-only at runtime
            return Task.FromResult(false);
        }

        public Task<IList<string>> GetRolesAsync(ApiUser user)
        {
            var player = server.GetPlayerByName(user.UserName);
            IList<string> roles;
            if (player.IsGuest)
            {
                roles = new[] { GunchoRoles.Guest };
            }
            else if (player.IsAdmin)
            {
                roles = new[] { GunchoRoles.User, GunchoRoles.Admin };
            }
            else
            {
                roles = new[] { GunchoRoles.User };
            }
            return Task.FromResult(roles);
        }

        public Task<bool> IsInRoleAsync(ApiUser user, string roleName)
        {
            var player = server.GetPlayerByName(user.UserName);
            switch (roleName)
            {
                case GunchoRoles.Guest:
                    return Task.FromResult(player.IsGuest);
                case GunchoRoles.Admin:
                    return Task.FromResult(player.IsAdmin);
                case GunchoRoles.User:
                    return Task.FromResult(!player.IsGuest);
                default:
                    throw new NotImplementedException();
            }
        }

        public Task RemoveFromRoleAsync(ApiUser user, string roleName)
        {
            // roles are read-only at runtime
            return Task.FromResult(false);
        }

        #endregion
    }
}
