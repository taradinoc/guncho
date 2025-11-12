using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Guncho.Client.Services;

public enum PlayConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting
}

public sealed class PlayConnectionService : IAsyncDisposable
{
    private readonly NavigationManager navigationManager;
    private readonly AuthService authService;
    private readonly ILogger<PlayConnectionService> logger;
    private readonly SemaphoreSlim connectionLock = new(1, 1);

    private HubConnection? hubConnection;
    private string? currentRealm;

    public PlayConnectionService(
        NavigationManager navigationManager,
        AuthService authService,
        ILogger<PlayConnectionService> logger)
    {
        this.navigationManager = navigationManager;
        this.authService = authService;
        this.logger = logger;
    }

    public PlayConnectionState ConnectionState { get; private set; } = PlayConnectionState.Disconnected;
    public bool ConnectionSlow { get; private set; }
    public string? CurrentRealm => currentRealm;

    public event Action? StateChanged;
    public event Action<string>? LineReceived;
    public event Action? GoodbyeReceived;

    public async Task ConnectToRealmAsync(string realm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(realm))
        {
            throw new ArgumentException("Realm must be provided", nameof(realm));
        }

        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        if (hubConnection == null)
        {
            throw new InvalidOperationException("Play hub connection is not available.");
        }

        await hubConnection.InvokeAsync("ConnectToRealmAsync", realm, cancellationToken).ConfigureAwait(false);
        currentRealm = realm;
    }

    public async Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (hubConnection == null || ConnectionState != PlayConnectionState.Connected)
        {
            throw new InvalidOperationException("The play connection is not active.");
        }

        await hubConnection.InvokeAsync("SendCommandAsync", command, cancellationToken).ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        await connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (hubConnection == null)
            {
                return;
            }

            try
            {
                await hubConnection.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to stop play hub connection cleanly.");
            }

            ConnectionState = PlayConnectionState.Disconnected;
            ConnectionSlow = false;
            currentRealm = null;
            NotifyStateChanged();
        }
        finally
        {
            connectionLock.Release();
        }
    }

    private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        await connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ConnectionState == PlayConnectionState.Connected || ConnectionState == PlayConnectionState.Connecting)
            {
                return;
            }

            if (hubConnection == null)
            {
                hubConnection = BuildHubConnection();
            }

            ConnectionState = PlayConnectionState.Connecting;
            ConnectionSlow = false;
            NotifyStateChanged();

            try
            {
                await hubConnection.StartAsync(cancellationToken).ConfigureAwait(false);
                ConnectionState = PlayConnectionState.Connected;
                ConnectionSlow = false;
                NotifyStateChanged();
            }
            catch
            {
                ConnectionState = PlayConnectionState.Disconnected;
                ConnectionSlow = false;
                NotifyStateChanged();
                throw;
            }
        }
        finally
        {
            connectionLock.Release();
        }
    }

    private HubConnection BuildHubConnection()
    {
        var hubUri = navigationManager.ToAbsoluteUri("/signalr/play");

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = async () => await authService.GetCurrentTokenAsync().ConfigureAwait(false);
            })
            .WithAutomaticReconnect()
            .Build();

        connection.Reconnecting += error =>
        {
            ConnectionState = PlayConnectionState.Reconnecting;
            ConnectionSlow = true;
            NotifyStateChanged();
            return Task.CompletedTask;
        };

        connection.Reconnected += connectionId =>
        {
            ConnectionState = PlayConnectionState.Connected;
            ConnectionSlow = false;
            NotifyStateChanged();
            return Task.CompletedTask;
        };

        connection.Closed += async error =>
        {
            ConnectionState = PlayConnectionState.Disconnected;
            ConnectionSlow = false;
            currentRealm = null;
            NotifyStateChanged();

            if (error != null)
            {
                logger.LogWarning(error, "Play hub connection closed unexpectedly.");
            }

            await Task.CompletedTask;
        };

        connection.On<string>("WriteLine", line =>
        {
            LineReceived?.Invoke(line);
            return Task.CompletedTask;
        });

        connection.On("Goodbye", () =>
        {
            GoodbyeReceived?.Invoke();
            return Task.CompletedTask;
        });

        return connection;
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection != null)
        {
            try
            {
                await hubConnection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to dispose play hub connection cleanly.");
            }
        }
    }
}
