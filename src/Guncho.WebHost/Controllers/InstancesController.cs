using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Guncho.WebHost.Controllers
{
    [Route("api/instances")]
    [Authorize]
    public sealed class InstancesController : GunchoApiController
    {
        // TODO: Implement instance management endpoints
        // This controller will handle game instance lifecycle operations
    }
}
