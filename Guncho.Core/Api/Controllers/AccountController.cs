using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNet.Identity;
using Guncho.Api.Security;

namespace Guncho.Api.Controllers
{
    public class RegistrationDto
    {
        [Required, MinLength(1), MaxLength(16)]
        [RegularExpression(@"(?i)^(?!guest)[a-z][-a-z0-9_]*$")]
        public string UserName;
        [Required, MinLength(8)]
        public string Password;
    }

    [RoutePrefix("api/account")]
    class AccountController : GunchoApiController
    {
        private readonly UserManager<ApiUser, int> userManager;

        public AccountController(UserManager<ApiUser, int> userManager)
        {
            this.userManager = userManager;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                userManager.Dispose();
            }

            base.Dispose(disposing);
        }

        [AllowAnonymous]
        [Route("")]
        public async Task<IHttpActionResult> Post(RegistrationDto registration)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = new ApiUser
            {
                UserName = registration.UserName,
            };

            var result = await userManager.CreateAsync(user, registration.Password);

            var errorResult = GetErrorResult(result);

            if (errorResult != null)
            {
                return errorResult;
            }

            return Ok();
        }

        // http://bitoftech.net/2014/06/01/token-based-authentication-asp-net-web-api-2-owin-asp-net-identity/
        private IHttpActionResult GetErrorResult(IdentityResult result)
        {
            if (result == null)
            {
                return InternalServerError();
            }

            if (!result.Succeeded)
            {
                if (result.Errors != null)
                {
                    foreach (string error in result.Errors)
                    {
                        ModelState.AddModelError("", error);
                    }
                }

                if (ModelState.IsValid)
                {
                    // No ModelState errors are available to send, so just return an empty BadRequest.
                    return BadRequest();
                }

                return BadRequest(ModelState);
            }

            return null;
        }
    }
}
