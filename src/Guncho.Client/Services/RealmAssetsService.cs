using System.Net.Http.Headers;
using System.Linq;
using System.Net.Http.Json;
using Guncho.Client.Auth;
using Guncho.Shared.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace Guncho.Client.Services;

public class RealmAssetsService
{
    private readonly HttpClient httpClient;
    private readonly JwtAuthenticationStateProvider authStateProvider;
    private readonly ILogger<RealmAssetsService> logger;

    public RealmAssetsService(HttpClient httpClient, AuthenticationStateProvider authStateProvider, ILogger<RealmAssetsService> logger)
    {
        this.httpClient = httpClient;
        this.authStateProvider = (JwtAuthenticationStateProvider)authStateProvider;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<RealmAssetSummaryDto>> GetManifestAsync(string realmName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(realmName))
        {
            return Array.Empty<RealmAssetSummaryDto>();
        }

        try
        {
            var manifest = await httpClient.GetFromJsonAsync<List<RealmAssetSummaryDto>>($"api/realms/{EncodeRealmName(realmName)}/assets/manifest", cancellationToken);
            return manifest ?? new List<RealmAssetSummaryDto>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch asset manifest for realm {RealmName}.", realmName);
            return Array.Empty<RealmAssetSummaryDto>();
        }
    }

    public async Task<RealmAssetDto?> GetAssetAsync(string realmName, string assetPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(realmName) || string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        try
        {
            return await httpClient.GetFromJsonAsync<RealmAssetDto>($"api/realms/{EncodeRealmName(realmName)}/assets/{BuildAssetRoute(assetPath)}", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch asset {AssetPath} for realm {RealmName}.", assetPath, realmName);
            return null;
        }
    }

    public async Task<AuthResult<RealmAssetDto>> UpsertAssetAsync(string realmName, string assetPath, RealmAssetUpdateDto update, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(realmName) || string.IsNullOrWhiteSpace(assetPath))
        {
            return AuthResult<RealmAssetDto>.Failure("Asset path is required.");
        }

        await EnsureAuthorizationAsync();

        try
        {
            var response = await httpClient.PutAsJsonAsync($"api/realms/{EncodeRealmName(realmName)}/assets/{BuildAssetRoute(assetPath)}", update, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response);
                return AuthResult<RealmAssetDto>.Failure(error ?? "Failed to save asset.");
            }

            var asset = await response.Content.ReadFromJsonAsync<RealmAssetDto>(cancellationToken: cancellationToken);
            return asset != null
                ? AuthResult<RealmAssetDto>.Success(asset)
                : AuthResult<RealmAssetDto>.Failure("Asset saved but server returned no content.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save asset {AssetPath} for realm {RealmName}.", assetPath, realmName);
            return AuthResult<RealmAssetDto>.Failure(ex.Message);
        }
    }

    public async Task<AuthResult<bool>> DeleteAssetAsync(string realmName, string assetPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(realmName) || string.IsNullOrWhiteSpace(assetPath))
        {
            return AuthResult<bool>.Failure("Asset path is required.");
        }

        await EnsureAuthorizationAsync();

        try
        {
            var response = await httpClient.DeleteAsync($"api/realms/{EncodeRealmName(realmName)}/assets/{BuildAssetRoute(assetPath)}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return AuthResult<bool>.Success(true);
            }

            var error = await ReadErrorAsync(response);
            return AuthResult<bool>.Failure(error ?? "Failed to delete asset.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete asset {AssetPath} for realm {RealmName}.", assetPath, realmName);
            return AuthResult<bool>.Failure(ex.Message);
        }
    }

    private async Task EnsureAuthorizationAsync()
    {
        // For cookie authentication, we don't need to set Authorization header
        // The cookie is sent automatically by the browser
        await Task.CompletedTask;
    }

    private static string EncodeRealmName(string realmName) => Uri.EscapeDataString(realmName);

    private static string BuildAssetRoute(string assetPath)
    {
        var trimmed = assetPath.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join('/', segments.Select(Uri.EscapeDataString));
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
