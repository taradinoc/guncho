using Guncho.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thinktecture.IdentityModel.Owin.ResourceAuthorization;

namespace Guncho.Api.Security
{
    interface IResourceAuthorizationManagerFactory
    {
        IResourceAuthorizationManager CreateResourceAuthorizationManager(IPlayersService playersService, IRealmsService realmsService);
    }
}
