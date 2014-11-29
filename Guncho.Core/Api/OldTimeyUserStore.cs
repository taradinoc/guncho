using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho.Api
{
    class OldTimeyUserStore : IUserStore<ApiUser, int>, IUserPasswordStore<ApiUser, int>,
        IUserRoleStore<ApiUser, int>, IUserClaimStore<ApiUser, int>
    {
        private readonly Server server;

        public OldTimeyUserStore(Server server)
        {
            this.server = server;
        }

        private ApiUser ToApiUser(Player player)
        {
            return new ApiUser(player.ID)
            {
                UserName = player.Name,
            };
        }

        #region IUserStore<ApiUser,int> Members

        public Task CreateAsync(ApiUser user)
        {
            var player = server.CreatePlayer(user.UserName, pwdSalt: "", pwdHash: "");
            return Task.FromResult(player);
        }

        public Task DeleteAsync(ApiUser user)
        {
            throw new NotImplementedException();
        }

        public Task<ApiUser> FindByIdAsync(int userId)
        {
            throw new NotImplementedException();
        }

        public Task<ApiUser> FindByNameAsync(string userName)
        {
            var player = server.GetPlayerByName(userName);
            return Task.FromResult(ToApiUser(player));
        }

        public Task UpdateAsync(ApiUser user)
        {
            throw new NotImplementedException();
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
            var parts = passwordHash.Split(new[] { ' ' }, 2);

            var player = server.GetPlayerByName(user.UserName);
            player.PasswordSalt = parts[0];
            player.PasswordHash = parts[1];

            return Task.Run(() => server.SavePlayers());
        }

        #endregion

        #region IUserRoleStore<ApiUser,int> Members

        public Task AddToRoleAsync(ApiUser user, string roleName)
        {
            throw new NotImplementedException();
        }

        public Task<IList<string>> GetRolesAsync(ApiUser user)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsInRoleAsync(ApiUser user, string roleName)
        {
            throw new NotImplementedException();
        }

        public Task RemoveFromRoleAsync(ApiUser user, string roleName)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IUserClaimStore<ApiUser,int> Members

        public Task AddClaimAsync(ApiUser user, System.Security.Claims.Claim claim)
        {
            throw new NotImplementedException();
        }

        public Task<IList<System.Security.Claims.Claim>> GetClaimsAsync(ApiUser user)
        {
            throw new NotImplementedException();
        }

        public Task RemoveClaimAsync(ApiUser user, System.Security.Claims.Claim claim)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
