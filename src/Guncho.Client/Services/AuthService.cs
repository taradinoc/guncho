using System.Net.Http.Json;
using System.Net.Http.Headers;
using Guncho.Client.Auth;
using Guncho.Shared.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace Guncho.Client.Services;

public class AuthService
{
    private readonly HttpClient httpClient;
    private readonly JwtAuthenticationStateProvider authStateProvider;
    private readonly ILogger<AuthService> logger;

    public AuthService(HttpClient httpClient, AuthenticationStateProvider authStateProvider, ILogger<AuthService> logger)
    {
        this.httpClient = httpClient;
        this.authStateProvider = (JwtAuthenticationStateProvider)authStateProvider;
        this.logger = logger;
    }

    public async Task<AuthResult<TokenResponseDto>> LoginAsync(LoginDto credentials)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/account/login", credentials);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response);
                return AuthResult<TokenResponseDto>.Failure(error ?? "Login failed.");
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                logger.LogWarning("Login succeeded but token response was empty.");
                return AuthResult<TokenResponseDto>.Failure("Login succeeded but no token was returned.");
            }

            await authStateProvider.MarkUserAsAuthenticated(
                tokenResponse.AccessToken,
                tokenResponse.UserName,
                tokenResponse.ExpiresIn);
            return AuthResult<TokenResponseDto>.Success(tokenResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login request failed.");
            return AuthResult<TokenResponseDto>.Failure(ex.Message);
        }
    }

    public async Task<AuthResult<bool>> RegisterAsync(RegistrationDto registration)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/account/register", registration);
            if (response.IsSuccessStatusCode)
            {
                return AuthResult<bool>.Success(true);
            }

            var error = await ReadErrorAsync(response);
            return AuthResult<bool>.Failure(error ?? "Registration failed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Registration request failed.");
            return AuthResult<bool>.Failure(ex.Message);
        }
    }

    public async Task<AuthResult<bool>> ChangePasswordAsync(PasswordChangeDto dto)
    {
        try
        {
            // ensure auth header attached
            await EnsureAuthorizationHeaderAsync();

            var response = await httpClient.PostAsJsonAsync("api/account/password", dto);
            if (response.IsSuccessStatusCode)
            {
                return AuthResult<bool>.Success(true);
            }

            var error = await ReadErrorAsync(response);
            return AuthResult<bool>.Failure(error ?? "Password change failed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Password change failed.");
            return AuthResult<bool>.Failure(ex.Message);
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            // Call server logout endpoint to clear the authentication cookie
            await httpClient.PostAsync("api/account/logout", null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to call logout endpoint, clearing local state anyway");
        }
        
        // Clear local state regardless of server response
        await authStateProvider.MarkUserAsLoggedOut();
    }

    public async Task<string?> GetCurrentTokenAsync()
    {
        return await authStateProvider.GetTokenAsync();
    }

    private async Task EnsureAuthorizationHeaderAsync()
    {
        // For cookie authentication, we don't need to set Authorization header
        // The cookie is sent automatically by the browser
        await Task.CompletedTask;
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        string? body;
        try
        {
            body = await response.Content.ReadAsStringAsync();
        }
        catch
        {
            body = null;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return response.ReasonPhrase;
        }

        return body;
    }
}

public record AuthResult<T>(bool IsSuccess, T? Value, string? Error)
{
    public static AuthResult<T> Success(T value) => new(true, value, null);
    public static AuthResult<T> Failure(string? error) => new(false, default, error);
}
