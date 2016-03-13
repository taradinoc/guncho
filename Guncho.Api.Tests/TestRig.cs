using Guncho.Services;
using System;
using System.Security.Claims;
using System.Security.Principal;

namespace Guncho.Api.Tests
{
    internal class TestRig
    {
        public TestRig()
        {
            PlayersService = new FakePlayersService();
            PlayersService.Add("Wizard", isAdmin: true);
            PlayersService.Add("Peon");

            RealmsService = new FakeRealmsService();
            RealmsService.Add("WizardRealm", "Wizard");
            RealmsService.Add("PeonRealm", "Peon");
        }

        public FakePlayersService PlayersService { get; private set; }
        public FakeRealmsService RealmsService { get; private set; }

        public IPrincipal WizardUser()
        {
            return MakePrincipal(PlayersService.GetPlayerByName("Wizard"));
        }

        public IPrincipal PeonUser()
        {
            return MakePrincipal(PlayersService.GetPlayerByName("Peon"));
        }

        private static IPrincipal MakePrincipal(Player player)
        {
            var claims = new Claim[]
            {
                new Claim(ClaimTypes.Name, player.Name),
            };

            return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationTypes.Basic));
        }
    }
}