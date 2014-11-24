using Guncho.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Guncho.Api
{
    public sealed class UserDto
    {
        public string Name;
    }

    [RoutePrefix("api/users")]
    public sealed class UsersController : ApiController
    {
        private readonly IPlayersService playersService;

        public UsersController(IPlayersService playersService)
        {
            this.playersService = playersService;
        }

        private UserDto MakeDto(Player p)
        {
            return new UserDto
            {
                Name = p.Name,
            };
        }

        [Route("")]
        public IEnumerable<UserDto> Get()
        {
            return playersService.GetAllPlayers().Select(p => MakeDto(p));
        }

        [Route("{name}", Name = "GetUserByName")]
        public UserDto GetByName(string name)
        {
            var player = playersService.GetPlayerByName(name);

            if (player == null)
            {
                throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
            }

            return MakeDto(player);
        }
    }
}
