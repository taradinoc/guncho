using System.Net.Http.Headers;
using System.Net.Http.Json;
using Guncho.Client.Auth;
using Guncho.Shared.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace Guncho.Client.Services;

public class RealmsService
{
    private readonly HttpClient httpClient;
    private readonly JwtAuthenticationStateProvider authStateProvider;
    private readonly ILogger<RealmsService> logger;

    public RealmsService(HttpClient httpClient, AuthenticationStateProvider authStateProvider, ILogger<RealmsService> logger)
    {
        this.httpClient = httpClient;
        this.authStateProvider = (JwtAuthenticationStateProvider)authStateProvider;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<RealmSummaryDto>> GetAllRealmsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<List<RealmSummaryDto>>("api/realms", cancellationToken);
            return response ?? new List<RealmSummaryDto>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch realm list.");
            return Array.Empty<RealmSummaryDto>();
        }
    }

    public async Task<IReadOnlyList<RealmFactoryDto>> GetRealmFactoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<List<RealmFactoryDto>>("api/realms/factories", cancellationToken);
            return response ?? new List<RealmFactoryDto>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch realm factories.");
            return Array.Empty<RealmFactoryDto>();
        }
    }

    public async Task<RealmDto?> GetRealmAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        try
        {
            return await httpClient.GetFromJsonAsync<RealmDto>($"api/realms/{Uri.EscapeDataString(name)}", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch realm {Realm}", name);
            return null;
        }
    }

    public async Task<AuthResult<RealmDto>> CreateRealmAsync(CreateRealmDto newRealm, CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizationAsync();

        try
        {
            var response = await httpClient.PostAsJsonAsync("api/realms", newRealm, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response);
                return AuthResult<RealmDto>.Failure(error ?? "Realm creation failed.");
            }

            var realm = await response.Content.ReadFromJsonAsync<RealmDto>(cancellationToken: cancellationToken);
            return realm != null
                ? AuthResult<RealmDto>.Success(realm)
                : AuthResult<RealmDto>.Failure("Realm created but server returned no content.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Realm creation threw an exception.");
            return AuthResult<RealmDto>.Failure(ex.Message);
        }
    }

    public async Task<AuthResult<bool>> UpdateRealmAsync(string name, RealmDto realm, CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizationAsync();

        try
        {
            var response = await httpClient.PutAsJsonAsync($"api/realms/{Uri.EscapeDataString(name)}", realm, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return AuthResult<bool>.Success(true);
            }

            var error = await ReadErrorAsync(response);
            return AuthResult<bool>.Failure(error ?? "Failed to update realm.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update realm {Realm}.", name);
            return AuthResult<bool>.Failure(ex.Message);
        }
    }

    public async Task<bool> DeleteRealmAsync(string name, CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizationAsync();

        try
        {
            var response = await httpClient.DeleteAsync($"api/realms/{Uri.EscapeDataString(name)}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete realm {Realm}.", name);
            return false;
        }
    }

    private async Task EnsureAuthorizationAsync()
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
