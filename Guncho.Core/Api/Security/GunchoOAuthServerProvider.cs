using Microsoft.Owin.Security.OAuth;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;

namespace Guncho.Api.Security
{
    public sealed class GunchoOAuthServerProvider : OAuthAuthorizationServerProvider
    {
        private readonly UserManager<ApiUser, int> userManager;

        public GunchoOAuthServerProvider(UserManager<ApiUser, int> userManager)
        {
            this.userManager = userManager;
        }

        public override Task ValidateClientAuthentication(OAuthValidateClientAuthenticationContext context)
        {
            context.Validated();
            return Task.FromResult(0);
        }

        public override async Task GrantResourceOwnerCredentials(OAuthGrantResourceOwnerCredentialsContext context)
        {
            context.OwinContext.Response.Headers.Add("Access-Control-Allow-Origin", new[] { "*" });

            ApiUser user = null;

            if (context.UserName != null && context.Password != null)
            {
                user = await userManager.FindAsync(context.UserName, context.Password);
            }

            if (user == null)
            {
                context.SetError("invalid_grant", "The user name or password is incorrect.");
                return;
            }

            var identity = new ClaimsIdentity(context.Options.AuthenticationType);
            identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName));
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
            // XXX include Admin role: identity.AddClaim(new Claim(ClaimTypes.Role, ...));

            context.Validated(identity);
        }
    }
}
