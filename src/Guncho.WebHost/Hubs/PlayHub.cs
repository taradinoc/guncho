using Microsoft.AspNetCore.SignalR;
using Guncho.WebHost.Services;

namespace Guncho.WebHost.Hubs
{
    /// <summary>
    /// Client-side methods that can be called from the server.
    /// </summary>
    public interface IPlayClient
    {
        Task WriteLine(string line);
        Task Goodbye();
    }

    /// <summary>
    /// SignalR hub for real-time game communication.
    /// </summary>
    public class PlayHub : Hub<IPlayClient>
    {
        private readonly ISignalRConnectionManager connectionManager;
        private readonly ILogger<PlayHub> logger;

        public PlayHub(ISignalRConnectionManager connectionManager, ILogger<PlayHub> logger)
        {
            this.connectionManager = connectionManager;
            this.logger = logger;
        }

        public override Task OnConnectedAsync()
        {
            logger.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);

            string? userName = Context.User?.Identity?.Name;
            connectionManager.NotifyConnectionAccepted(Context.ConnectionId, userName);
            
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            logger.LogDebug("Client disconnected: {ConnectionId}, Exception: {Exception}", 
                Context.ConnectionId, exception?.Message);
            
            connectionManager.NotifyConnectionClosed(Context.ConnectionId);
            
            return base.OnDisconnectedAsync(exception);
        }

        public Task SendCommandAsync(string command)
        {
            logger.LogDebug("Command received from {ConnectionId}: {Command}", 
                Context.ConnectionId, command);

            var connection = connectionManager.GetConnectionById(Context.ConnectionId);
            if (connection != null)
            {
                connection.EnqueueCommand(command);
            }
            else
            {
                logger.LogWarning("Connection not found: {ConnectionId}", Context.ConnectionId);
            }

            return Task.CompletedTask;
        }

        public Task ConnectToRealmAsync(string realmName)
        {
            logger.LogDebug("ConnectToRealm request from {ConnectionId}: {RealmName}", 
                Context.ConnectionId, realmName);

            var connection = connectionManager.GetConnectionById(Context.ConnectionId);
            if (connection != null)
            {
                // Only send the "connect" command if not already logged in
                // Authenticated users are auto-logged in and should use @teleport to switch realms
                if (connection.Player == null)
                {
                    connection.EnqueueCommand($"connect {realmName}");
                }
                else
                {
                    logger.LogDebug("Player already connected, ignoring ConnectToRealmAsync call");
                }
            }
            else
            {
                logger.LogWarning("Connection not found: {ConnectionId}", Context.ConnectionId);
            }

            return Task.CompletedTask;
        }
    }
}
