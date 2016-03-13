using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNet.Identity;
using Guncho.Api.Security;
using System.Text.RegularExpressions;
using Guncho.Services;

namespace Guncho.Api.Controllers
{
    public class RegistrationDto
    {
        [Required, MinLength(3), MaxLength(16)]
        [RegularExpression(PlayersServiceConstants.UserNameRegexPattern)]
        public string UserName { get; set; }
        [Required, MinLength(8)]
        public string Password { get; set; }
    }

    public class PasswordChangeDto
    {
        public string OldPassword { get; set; }

        [Required, MinLength(8)]
        public string NewPassword { get; set; }

        [Required, MinLength(8)]
        [Compare("NewPassword", ErrorMessage = "New passwords must match")]
        public string ConfirmNewPassword { get; set; }
    }

    [RoutePrefix("api/account")]
    public class AccountController : GunchoApiController
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

            return CreatedAtRoute("GetProfileByName", new { name = user.UserName }, "");
        }

        [Route("password/{name}")]
        public async Task<IHttpActionResult> PostPasswordByName(string name, PasswordChangeDto passwords)
        {
            if (!Request.CheckAccess(
                GunchoResources.UserActions.Edit,
                GunchoResources.User, name,
                GunchoResources.Field, GunchoResources.UserFields.Password))
            {
                return Forbidden();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = userManager.FindByName(name);
            var result = await userManager.ChangePasswordAsync(user.Id, passwords.OldPassword, passwords.NewPassword);
            var errorResult = GetErrorResult(result);

            if (errorResult != null)
            {
                return errorResult;
            }

            return NoContent();
        }

        [Route("password/my")]
        public Task<IHttpActionResult> PostMyPassword(PasswordChangeDto passwords)
        {
            return PostPasswordByName(User.Identity.Name, passwords);
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
