using Guncho.Api.Security;
using Guncho.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Guncho.Api.Controllers
{
    public class ProfileDto
    {
        public string Name;
        public string Uri;
        public IDictionary<string, string> Attributes;
    }

    [RoutePrefix("api/profiles")]
    public sealed class ProfilesController : GunchoApiController
    {
        private static readonly string[] writableAttributes =
        {
            // A human-readable description, shown when examining the player.
            "description",

            // A human-readable gender, e.g. "male", "female", "other"
            "gender",
            // 1st person pronouns, e.g. "s=I|o=me|p=my|ps=mine|r=myself|v=am"
            "pronouns_1p",
            // 2nd person pronouns, e.g. "s=you|o=you|p=your|ps=yours|r=yourself|v=are"
            "pronouns_2p",
            // 3rd person pronouns, e.g. "s=he|o=him|p=his|ps=his|r=himself|v=is"
            "pronouns_3p",
        }; 
        
        private readonly IPlayersService playersService;

        public ProfilesController(IPlayersService playersService)
        {
            this.playersService = playersService;
        }

        private ProfileDto MakeDto(Player p)
        {
            var result = new ProfileDto
            {
                Name = p.Name,
                Uri = Url.Link("GetProfileByName", new { name = p.Name }),
                Attributes = new Dictionary<string, string>(),
            };

            foreach (var pair in p.GetAllAttributes())
            {
                if (Request.CheckAccess(
                    GunchoResources.UserActions.View,
                    GunchoResources.User, p.Name,
                    GunchoResources.Attribute, pair.Key))
                {
                    result.Attributes.Add(pair);
                }
            }

            return result;
        }

        [Route("")]
        public IEnumerable<ProfileDto> Get()
        {
            return playersService.GetAllPlayers().Select(p => MakeDto(p));
        }

        [Route("{name}", Name = "GetProfileByName")]
        public IHttpActionResult GetProfileByName(string name)
        {
            var player = playersService.GetPlayerByName(name);

            if (player == null)
            {
                return NotFound();
            }

            return Ok(MakeDto(player));
        }

        [Route("{name}", Name = "PutProfileByName")]
        public IHttpActionResult PutProfileByName(string name, [FromBody] ProfileDto newProfile)
        {
            var player = playersService.GetPlayerByName(name);

            if (player == null)
            {
                return NotFound();
            }

            var checks = new Queue<Func<Player, bool>>();
            var updates = new Queue<Action<Player>>();
            // TODO: don't modify Player objects, do everything through service methods

            if (newProfile.Name != null && newProfile.Name != player.Name)
            {
                checks.Enqueue(p =>
                    playersService.IsValidNameChange(p.Name, newProfile.Name) &&
                    Request.CheckAccess(
                        GunchoResources.UserActions.Edit,
                        GunchoResources.User, p.Name,
                        GunchoResources.Field, GunchoResources.UserFields.Name));
                updates.Enqueue(p => p.Name = newProfile.Name);
            }

            if (newProfile.Attributes != null)
            {
                foreach (var pair in newProfile.Attributes)
                {
                    var key = pair.Key;
                    var value = pair.Value;

                    if (!writableAttributes.Contains(key))
                    {
                        ModelState.AddModelError("Attributes", string.Format("Attribute {0} is not writable.", key));
                        continue;
                    }

                    if (value == null || value.Length == 0)
                    {
                        checks.Enqueue(p =>
                            Request.CheckAccess(
                                GunchoResources.AttributeActions.Delete,
                                GunchoResources.User, p.Name,
                                GunchoResources.Attribute, key));
                    }
                    else
                    {
                        checks.Enqueue(p =>
                            Request.CheckAccess(
                                GunchoResources.AttributeActions.Delete,
                                GunchoResources.User, p.Name,
                                GunchoResources.Attribute, key));
                    }

                    updates.Enqueue(p => p.SetAttribute(key, value));
                }
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = playersService.TransactionalUpdate(
                player,
                p =>
                {
                    foreach (var check in checks)
                    {
                        if (!check(p))
                        {
                            return false;
                        }
                    }

                    foreach (var update in updates)
                    {
                        update(p);
                    }

                    return true;
                });

            if (result == false)
            {
                return BadRequest(ModelState);
            }

            return GetProfileByName(name);
        }

        [Route("my")]
        public IHttpActionResult GetMy()
        {
            return GetProfileByName(User.Identity.Name);
        }

        [Route("my")]
        public IHttpActionResult PutMy(ProfileDto newProfile)
        {
            return PutProfileByName(User.Identity.Name, newProfile);
        }

        private bool ValidateAttributeWrites(IDictionary<string, string> attributes)
        {
            return attributes == null || attributes.Keys.All(k => writableAttributes.Contains(k));
        }
    }
}
