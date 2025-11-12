using Microsoft.AspNetCore.Mvc;

namespace Guncho.WebHost.Controllers
{
    /// <summary>
    /// Base controller for Guncho API endpoints.
    /// Provides common helper methods for ASP.NET Core controllers.
    /// </summary>
    [ApiController]
    public abstract class GunchoApiController : ControllerBase
    {
        // ASP.NET Core already provides NotFound(), Ok(), BadRequest(), etc.
        // Add custom result helpers as needed

        protected IActionResult Forbidden()
        {
            return StatusCode(403);
        }

        protected new IActionResult UnprocessableEntity()
        {
            return StatusCode(422);
        }

        protected IActionResult UnsupportedMediaType()
        {
            return StatusCode(415);
        }
    }
}
