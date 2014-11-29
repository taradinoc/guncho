using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thinktecture.IdentityModel.Owin.ResourceAuthorization;

namespace Guncho.Api
{
    public sealed class GunchoResourceAuthorization : IResourceAuthorizationManager
    {
        public Task<bool> CheckAccessAsync(ResourceAuthorizationContext context)
        {
            throw new NotImplementedException();
        }
    }
}
