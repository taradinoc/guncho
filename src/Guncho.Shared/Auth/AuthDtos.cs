using System.ComponentModel.DataAnnotations;

namespace Guncho.Shared.Auth;

public class RegistrationDto
{
    [Required, MinLength(3), MaxLength(16)]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$", ErrorMessage = "Username must start with a letter and contain only letters, numbers, and underscores")]
    public string UserName { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class PasswordChangeDto
{
    public string? OldPassword { get; set; }

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, MinLength(8)]
    [Compare("NewPassword", ErrorMessage = "New passwords must match")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class TokenResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string UserName { get; set; } = string.Empty;
}
