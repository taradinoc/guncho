using System.Net.Http.Headers;
using System.Net.Http.Json;
using Guncho.Client.Auth;
using Guncho.Shared.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace Guncho.Client.Services;

public class PlayerService
{
    private readonly HttpClient httpClient;
    private readonly JwtAuthenticationStateProvider authStateProvider;
    private readonly ILogger<PlayerService> logger;

    public PlayerService(HttpClient httpClient, AuthenticationStateProvider authStateProvider, ILogger<PlayerService> logger)
    {
        this.httpClient = httpClient;
        this.authStateProvider = (JwtAuthenticationStateProvider)authStateProvider;
        this.logger = logger;
    }

    public async Task<PlayerDto?> GetCurrentPlayerAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizationAsync();

        try
        {
            return await httpClient.GetFromJsonAsync<PlayerDto>("api/players/me", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch current player profile.");
            return null;
        }
    }

    public async Task<PlayerDto?> GetPlayerByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        try
        {
            return await httpClient.GetFromJsonAsync<PlayerDto>($"api/players/{Uri.EscapeDataString(name)}", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch player '{PlayerName}'.", name);
            return null;
        }
    }

    public async Task<bool> UpdatePlayerAsync(PlayerDto player, CancellationToken cancellationToken = default)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        await EnsureAuthorizationAsync();

        try
        {
            var response = await httpClient.PutAsJsonAsync($"api/players/{player.Id}", player, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update player {PlayerId}.", player.Id);
            return false;
        }
    }

    private async Task EnsureAuthorizationAsync()
    {
        // For cookie authentication, we don't need to set Authorization header
        // The cookie is sent automatically by the browser
        await Task.CompletedTask;
    }
}
