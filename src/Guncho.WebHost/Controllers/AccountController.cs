using Guncho.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Guncho.WebHost.Controllers
{
    public class LoginDto
    {
        [Required]
        public required string UserName { get; set; }

        [Required]
        public required string Password { get; set; }
    }

    public class RegistrationDto
    {
        [Required, MinLength(3), MaxLength(16)]
        [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$")]
        public required string UserName { get; set; }

        [Required, MinLength(8)]
        public required string Password { get; set; }
    }

    public class PasswordChangeDto
    {
        public required string OldPassword { get; set; }

        [Required, MinLength(8)]
        public required string NewPassword { get; set; }

        [Required, MinLength(8)]
        [Compare("NewPassword", ErrorMessage = "New passwords must match")]
        public required string ConfirmNewPassword { get; set; }
    }

    [Route("api/account")]
    public class AccountController : GunchoApiController
    {
        private readonly IPlayerService _playerService;
        private readonly IConfiguration _configuration;

        public AccountController(IPlayerService playerService, IConfiguration configuration)
        {
            _playerService = playerService;
            _configuration = configuration;
        }

        private string GenerateJwtToken(Player player)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, player.Name),
                new Claim(ClaimTypes.NameIdentifier, player.ID.ToString()),
                new Claim("name", player.Name), // For JWT standard claim
            };

            if (player.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            // Use a key from configuration or generate one
            var jwtSecret = _configuration["Guncho:WebAuthSecret"] ?? "default-secret-key-change-this-in-production-make-it-at-least-32-chars";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "Guncho",
                audience: "Guncho.Client",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(30),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [AllowAnonymous]
        [HttpPost("")]
        public async Task<IActionResult> PostAsync([FromBody] RegistrationDto registration)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if user already exists
            var existing = await _playerService.GetPlayerByNameAsync(registration.UserName);
            if (existing != null)
            {
                return BadRequest(new { Message = "Username already exists" });
            }

            // Create new player
            var nextId = _playerService.GetAllPlayers().Any() 
                ? _playerService.GetAllPlayers().Max(p => p.ID) + 1 
                : 1;

            var player = new Player(nextId, registration.UserName, isAdmin: false, isGuest: false)
            {
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registration.Password),
                PasswordSalt = "" // BCrypt includes salt in hash
            };

            await _playerService.SavePlayerAsync(player);

            return CreatedAtRoute("GetProfileByName", new { name = player.Name }, new { UserName = player.Name });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> PostLoginAsync([FromBody] LoginDto login)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var player = await _playerService.GetPlayerByNameAsync(login.UserName.ToLower());
            if (player == null)
            {
                return Unauthorized(new { Message = "Invalid username or password" });
            }

            // Verify password using the service (supports both old and new formats)
            if (!await _playerService.ValidateLogInAsync(player, login.Password))
            {
                return Unauthorized(new { Message = "Invalid username or password" });
            }

            // Create claims and sign in
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, player.Name),
                new Claim(ClaimTypes.NameIdentifier, player.ID.ToString()),
            };

            if (player.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // For cookie auth, just return username - client doesn't need a token
            // The cookie will be sent automatically with requests
            return Ok(new { 
                AccessToken = player.Name, // Use username as a pseudo-token for client state
                TokenType = "Cookie",
                ExpiresIn = 2592000, // 30 days in seconds
                UserName = player.Name
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> PostLogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { Message = "Logged out successfully" });
        }

        [Authorize]
        [HttpPost("password/{name}")]
        public async Task<IActionResult> PostPasswordByNameAsync(string name, [FromBody] PasswordChangeDto passwords)
        {
            // TODO: Implement authorization check
            if (User?.Identity?.Name != name)
            {
                return Forbidden();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var player = await _playerService.GetPlayerByNameAsync(name);
            if (player == null)
            {
                return NotFound();
            }

            // Verify old password
            if (!BCrypt.Net.BCrypt.Verify(passwords.OldPassword, player.PasswordHash))
            {
                return BadRequest(new { Message = "Old password is incorrect" });
            }

            // Update password
            player.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwords.NewPassword);
            await _playerService.SavePlayerAsync(player);

            return NoContent();
        }
    }
}
