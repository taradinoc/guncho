using Guncho.Api.Security;
using Guncho.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Thinktecture.IdentityModel.Owin.ResourceAuthorization;

namespace Guncho.Api.Security
{
    public sealed class GunchoResourceAuthorization : ResourceAuthorizationManager
    {
        private readonly IPlayersService playersService;
        private readonly IRealmsService realmsService;

        public GunchoResourceAuthorization(IPlayersService playersService, IRealmsService realmsService)
        {
            this.playersService = playersService;
            this.realmsService = realmsService;
        }

        public override Task<bool> CheckAccessAsync(ResourceAuthorizationContext context)
        {
            var resource = context.Resource.First().Value;

            switch (resource)
            {
                case GunchoResources.Realm:
                    return CheckRealmAccessAsync(context);

                case GunchoResources.User:
                    return CheckUserAccessAsync(context);
            }

            return Nok();
        }

        #region Realm Access

        private Task<bool> CheckRealmAccessAsync(ResourceAuthorizationContext context)
        {
            var action = context.Action.First().Value;

            var realmName = context.Resource.Skip(1).Take(1).Single().Value;
            var realm = realmsService.GetRealmByName(realmName);
            if (realm == null)
            {
                return Nok();
            }

            switch (action)
            {
                case GunchoResources.RealmActions.EditAssets:
                    return CheckRealmEditAssetsAccessAsync(context, realm);

                case GunchoResources.RealmActions.ViewAssets:
                    return CheckRealmViewAssetsAccessAsync(context, realm);

                case GunchoResources.RealmActions.ListRealm:
                case GunchoResources.RealmActions.ViewDetails:
                    return CheckRealmVisibilityAccessAsync(context, realm);
            }

            return Nok();
        }

        private bool HasRealmAccessLevel(ResourceAuthorizationContext context, Realm realm, RealmAccessLevel level)
        {
            var actor = GetActor(context);

            if (actor == null)
            {
                return false;
            }

            return realm.GetAccessLevel(actor) >= level;
        }

        private Task<bool> CheckRealmVisibilityAccessAsync(ResourceAuthorizationContext context, Realm realm)
        {
            return Eval(HasRealmAccessLevel(context, realm, RealmAccessLevel.Visible));
        }

        private Task<bool> CheckRealmViewAssetsAccessAsync(ResourceAuthorizationContext context, Realm realm)
        {
            return Eval(HasRealmAccessLevel(context, realm, RealmAccessLevel.ViewSource));
        }

        private Task<bool> CheckRealmEditAssetsAccessAsync(ResourceAuthorizationContext context, Realm realm)
        {
            return Eval(HasRealmAccessLevel(context, realm, RealmAccessLevel.EditSource));
        }

        #endregion

        #region User Access

        private Task<bool> CheckUserAccessAsync(ResourceAuthorizationContext context)
        {
            var action = context.Action.First().Value;

            var victimName = context.Resource.Skip(1).Take(1).Single().Value;
            var victim = playersService.GetPlayerByName(victimName);
            if (victim == null)
            {
                return Nok();
            }

            switch (action)
            {
                case GunchoResources.UserActions.EditProfile:
                    return CheckUserEditProfileAccessAsync(context, victim);
            }

            return Nok();
        }

        private Task<bool> CheckUserEditProfileAccessAsync(ResourceAuthorizationContext context, Player victim)
        {
            var actor = GetActor(context);
            return Eval(actor == victim || actor.IsAdmin);
        }

        private Player GetActor(ResourceAuthorizationContext context)
        {
            if (!context.Principal.Identity.IsAuthenticated)
            {
                return null;
            }

            // TODO: use NameIdentifier instead of Name once GetPlayerById is less braindead
            var claim = context.Principal.FindFirst(ClaimTypes.Name);
            if (claim == null)
            {
                return null;
            }

            return playersService.GetPlayerByName(claim.Value);
        }

        #endregion
    }
}
