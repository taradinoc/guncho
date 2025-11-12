using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace Guncho.Client.Auth;

public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient httpClient;
    private readonly ILocalStorageService localStorage;
    private const string TokenKey = "authToken";

    public JwtAuthenticationStateProvider(HttpClient httpClient, ILocalStorageService localStorage)
    {
        this.httpClient = httpClient;
        this.localStorage = localStorage;
    }

    public async Task<string?> GetTokenAsync()
    {
        var token = await localStorage.GetItemAsync<string>(TokenKey);
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await localStorage.GetItemAsync<string>(TokenKey);

        if (string.IsNullOrWhiteSpace(token))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // For cookie auth, the "token" is just the username
        // Don't set Authorization header - cookies are sent automatically
        // httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, token),
            new Claim("name", token)
        };

        var identity = new ClaimsIdentity(claims, "cookie");
        var user = new ClaimsPrincipal(identity);

        return new AuthenticationState(user);
    }

    public async Task MarkUserAsAuthenticated(string token, string userName)
    {
        await localStorage.SetItemAsync(TokenKey, token);
        // Don't set Authorization header for cookie auth
        // httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // For cookie auth, just create claims from the username
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, userName),
            new Claim("name", userName)
        };

        var identity = new ClaimsIdentity(claims, "cookie");
        var user = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public async Task MarkUserAsLoggedOut()
    {
        await localStorage.RemoveItemAsync(TokenKey);
        // Don't mess with headers for cookie auth
        // httpClient.DefaultRequestHeaders.Authorization = null;

        var identity = new ClaimsIdentity();
        var user = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

        if (keyValuePairs == null)
        {
            return Enumerable.Empty<Claim>();
        }

        var claims = new List<Claim>();
        
        foreach (var kvp in keyValuePairs)
        {
            // Map JWT claim names to ClaimTypes
            var claimType = kvp.Key switch
            {
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name" => ClaimTypes.Name,
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" => ClaimTypes.NameIdentifier,
                "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" => ClaimTypes.Role,
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress" => ClaimTypes.Email,
                "name" or "unique_name" => ClaimTypes.Name,
                "nameid" => ClaimTypes.NameIdentifier,
                "role" => ClaimTypes.Role,
                "email" => ClaimTypes.Email,
                _ => kvp.Key
            };

            var value = kvp.Value?.ToString() ?? "";
            
            // Handle array values (like multiple roles)
            if (kvp.Value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    claims.Add(new Claim(claimType, item.ToString()));
                }
            }
            else
            {
                claims.Add(new Claim(claimType, value));
            }
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
