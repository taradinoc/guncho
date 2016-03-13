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

            if (action == GunchoResources.RealmActions.Create)
            {
                return CheckRealmCreateAccessAsync(context, realmName);
            }

            var realm = realmsService.GetRealmByName(realmName);
            if (realm == null)
            {
                return Nok();
            }

            var nextResource = context.Resource.Skip(2).FirstOrDefault();
            if (nextResource != null && nextResource.Value == GunchoResources.Asset)
            {
                var asset = context.Resource.Skip(3).First().Value;
                return CheckRealmAssetAccessAsync(context, realm, asset);
            }

            switch (action)
            {
                case GunchoResources.RealmActions.EnableDisable:
                    return CheckRealmEnableDisableAccessAsync(context, realm);

                case GunchoResources.RealmActions.Edit:
                    return CheckRealmEditAssetsAccessAsync(context, realm);

                case GunchoResources.RealmActions.Join:
                    return CheckRealmJoinAccessAsync(context, realm);

                case GunchoResources.RealmActions.Teleport:
                    return CheckRealmTeleportAccessAsync(context, realm);

                case GunchoResources.RealmActions.List:
                case GunchoResources.RealmActions.View:
                case GunchoResources.RealmActions.ViewHistory:
                    return CheckRealmVisibilityAccessAsync(context, realm);
            }

            return Nok();
        }

        private Task<bool> CheckRealmAssetAccessAsync(ResourceAuthorizationContext context, Realm realm, string path)
        {
            var action = context.Action.First().Value;

            switch (action)
            {
                case GunchoResources.AssetActions.Create:
                case GunchoResources.AssetActions.Import:
                    //XXX
                    return Nok();

                case GunchoResources.AssetActions.Delete:
                case GunchoResources.AssetActions.Edit:
                    return Eval(HasRealmAccessLevel(context, realm, RealmAccessLevel.EditSource));

                case GunchoResources.AssetActions.Share:
                    return Eval(HasRealmAccessLevel(context, realm, RealmAccessLevel.EditAccess));

                case GunchoResources.AssetActions.List:
                case GunchoResources.AssetActions.View:
                case GunchoResources.AssetActions.ViewHistory:
                    return Eval(HasRealmAccessLevel(context, realm, RealmAccessLevel.ViewSource));
            }

            return Nok();
        }

        private Task<bool> CheckRealmTeleportAccessAsync(ResourceAuthorizationContext context, Realm realm)
        {
            return Eval(HasRealmAccessLevel(context, realm, RealmAccessLevel.Invited));
        }

        private Task<bool> CheckRealmJoinAccessAsync(ResourceAuthorizationContext context, Realm realm)
        {
            return Eval(HasRealmAccessLevel(context, realm, RealmAccessLevel.Banned + 1));
        }

        private Task<bool> CheckRealmEnableDisableAccessAsync(ResourceAuthorizationContext context, Realm realm)
        {
            var actor = GetActor(context);
            return Eval(actor != null && (actor == realm.Owner || actor.IsAdmin));
        }

        private Task<bool> CheckRealmCreateAccessAsync(ResourceAuthorizationContext context, string realmName)
        {
            var actor = GetActor(context);
            return Eval(actor != null && !actor.IsGuest);
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

            var nextResource = context.Resource.Skip(2).FirstOrDefault();
            if (nextResource != null)
            {
                if (nextResource.Value == GunchoResources.Attribute)
                {
                    var attribute = context.Resource.Skip(3).First().Value;
                    return CheckUserAttributeAccessAsync(context, victim, attribute);
                }
                else if (nextResource.Value == GunchoResources.Field)
                {
                    var field = context.Resource.Skip(3).First().Value;
                    return CheckUserFieldAccessAsync(context, victim, field);
                }
            }

            switch (action)
            {
                case GunchoResources.UserActions.Create:
                case GunchoResources.UserActions.Edit:
                    return CheckUserEditProfileAccessAsync(context, victim);

                case GunchoResources.UserActions.EnableDisable:
                    return CheckUserEditInternalsAsync(context, victim);
            }

            return Nok();
        }

        private Task<bool> CheckUserAttributeAccessAsync(ResourceAuthorizationContext context, Player victim, string attribute)
        {
            var actor = GetActor(context);
            var action = context.Action.First().Value;

            switch (action)
            {
                case GunchoResources.AttributeActions.Delete:
                case GunchoResources.AttributeActions.Edit:
                    return Eval(actor != null && (actor == victim || actor.IsAdmin));

                case GunchoResources.AttributeActions.View:
                    return Ok();
            }

            return Nok();
        }

        private Task<bool> CheckUserFieldAccessAsync(ResourceAuthorizationContext context, Player victim, string field)
        {
            switch (field)
            {
                case GunchoResources.UserFields.Password:
                    return CheckUserEditProfileAccessAsync(context, victim);

                case GunchoResources.UserFields.Name:
                    return CheckUserEditInternalsAsync(context, victim);
            }

            return Nok();
        }

        private Task<bool> CheckUserEditInternalsAsync(ResourceAuthorizationContext context, Player victim)
        {
            var actor = GetActor(context);
            return Eval(actor != null && actor.IsAdmin);
        }

        private Task<bool> CheckUserEditProfileAccessAsync(ResourceAuthorizationContext context, Player victim)
        {
            var actor = GetActor(context);
            return Eval(actor == victim || (actor != null && actor.IsAdmin));
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
