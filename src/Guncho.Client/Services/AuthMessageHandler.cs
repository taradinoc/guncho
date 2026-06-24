using System.Net;
using Guncho.Client.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Guncho.Client.Services;

/// <summary>
/// Detects 401 Unauthorized responses from API calls and clears stale client-side
/// authentication state. This handles the case where the server-side auth cookie has
/// expired but the client still thinks the user is logged in (because the username
/// is still in localStorage).
/// </summary>
public class AuthMessageHandler : DelegatingHandler
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<AuthMessageHandler> logger;

    public AuthMessageHandler(IServiceProvider serviceProvider, ILogger<AuthMessageHandler> logger)
        : base(new HttpClientHandler())
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        var path = request.RequestUri?.AbsolutePath ?? "";
        var isAuthEndpoint = path.StartsWith("/api/account/", StringComparison.OrdinalIgnoreCase);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthEndpoint)
        {
            // Don't clear auth state for auth-related endpoints (login, register, logout).
            // A failed login shouldn't log out an existing session, and logout already
            // clears state explicitly.
            logger.LogWarning("Received 401 for {Path} — clearing stale client auth state", path);

            var authStateProvider = serviceProvider.GetRequiredService<JwtAuthenticationStateProvider>();
            await authStateProvider.MarkUserAsLoggedOut();
        }
        else if (response.IsSuccessStatusCode && !isAuthEndpoint && path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            // Slide the client-side expiration window on successful API calls,
            // matching the server's SlidingExpiration cookie behavior.
            var authStateProvider = serviceProvider.GetRequiredService<JwtAuthenticationStateProvider>();
            await authStateProvider.SlideExpirationAsync();
        }

        return response;
    }
}
