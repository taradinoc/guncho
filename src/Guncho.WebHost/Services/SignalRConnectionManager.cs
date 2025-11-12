using Guncho.WebHost.Hubs;
using Guncho.WebHost.Connections;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Guncho.WebHost.Services
{
    public interface ISignalRConnectionManager
    {
        SignalRConnection? GetConnectionById(string connectionId);
        Task SendToClientAsync(string connectionId, string line);
        Task TerminateClientAsync(string connectionId);
        void NotifyConnectionAccepted(string connectionId, string? playerName = null);
        void NotifyConnectionClosed(string connectionId);
        
        event EventHandler<ConnectionAcceptedEventArgs> ConnectionAccepted;
        event EventHandler<ConnectionClosedEventArgs> ConnectionClosed;
    }

    public class ConnectionAcceptedEventArgs : EventArgs
    {
        public SignalRConnection Connection { get; set; } = null!;
        public string AuthenticatedUserName { get; set; } = null!;
    }

    public class ConnectionClosedEventArgs : EventArgs
    {
        public SignalRConnection Connection { get; set; } = null!;
    }

    public class SignalRConnectionManager : ISignalRConnectionManager
    {
        private readonly IHubContext<PlayHub, IPlayClient> hubContext;
        private readonly ILogger<SignalRConnectionManager> logger;
        private readonly ConcurrentDictionary<string, SignalRConnection> connections = new();

        public SignalRConnectionManager(
            IHubContext<PlayHub, IPlayClient> hubContext,
            ILogger<SignalRConnectionManager> logger)
        {
            this.hubContext = hubContext;
            this.logger = logger;
        }

        public event EventHandler<ConnectionAcceptedEventArgs>? ConnectionAccepted;
        public event EventHandler<ConnectionClosedEventArgs>? ConnectionClosed;

        public SignalRConnection? GetConnectionById(string connectionId)
        {
            connections.TryGetValue(connectionId, out var result);
            return result;
        }

        public async Task SendToClientAsync(string connectionId, string line)
        {
            await hubContext.Clients.Client(connectionId).WriteLine(line);
        }

        public async Task TerminateClientAsync(string connectionId)
        {
            var connection = GetConnectionById(connectionId);
            await hubContext.Clients.Client(connectionId).Goodbye();
            
            if (connection != null)
            {
                await connection.WhenClosed();
            }
        }

        public void NotifyConnectionAccepted(string connectionId, string? playerName = null)
        {
            logger.LogInformation("Connection accepted: {ConnectionId}, User: {UserName}", 
                connectionId, playerName ?? "Guest");

            var connection = new SignalRConnection(this, connectionId);
            connections[connectionId] = connection;
            
            ConnectionAccepted?.Invoke(this, new ConnectionAcceptedEventArgs
            {
                Connection = connection,
                AuthenticatedUserName = playerName ?? "Guest"
            });
        }

        public void NotifyConnectionClosed(string connectionId)
        {
            logger.LogInformation("Connection closed: {ConnectionId}", connectionId);

            if (connections.TryRemove(connectionId, out var connection))
            {
                connection.NotifyClosed();
                ConnectionClosed?.Invoke(this, new ConnectionClosedEventArgs
                {
                    Connection = connection
                });
            }
        }
    }
}
