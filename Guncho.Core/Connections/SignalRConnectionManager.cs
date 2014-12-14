using Guncho.Api.Hubs;
using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Guncho.Connections
{
    public interface ISignalRConnectionManager : IConnectionManager<SignalRConnection>
    {
        SignalRConnection GetConnectionById(string connectionId);
        Task SendToClientAsync(string connectionId, string line);
        Task TerminateClientAsync(string ConnectionId);
        void NotifyConnectionAccepted(string connectionId, string playerName = null);
        void NotifyConnectionClosed(string connectionId);
    }

    internal sealed class SignalRConnectionManager : ISignalRConnectionManager
    {
        private IHubContext<IClient> hubContext;

        private readonly ConcurrentDictionary<string, SignalRConnection> connections = new ConcurrentDictionary<string, SignalRConnection>();

        public SignalRConnectionManager()
        {
            ConnectionAccepted += delegate { };
            ConnectionClosed += delegate { };
        }

        public async Task Run(CancellationToken cancellationToken)
        {
            hubContext = GlobalHost.ConnectionManager.GetHubContext<PlayHub, IClient>();

            // run until cancelled
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(() => { tcs.SetResult(true); });
            await tcs.Task;
        }

        public event EventHandler<ConnectionAcceptedEventArgs<SignalRConnection>> ConnectionAccepted;
        public event EventHandler<ConnectionEventArgs<SignalRConnection>> ConnectionClosed;

        public SignalRConnection GetConnectionById(string connectionId)
        {
            SignalRConnection result;
            connections.TryGetValue(connectionId, out result);
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

        public void NotifyConnectionAccepted(string connectionId, string playerName = null)
        {
            var connection = new SignalRConnection(this, connectionId);
            connections[connectionId] = connection;
            ConnectionAccepted(this, new ConnectionAcceptedEventArgs<SignalRConnection>()
            {
                Connection = connection,
                AuthenticatedUserName = playerName ?? "Guest"
            });
        }

        public void NotifyConnectionClosed(string connectionId)
        {
            SignalRConnection connection;
            if (connections.TryRemove(connectionId, out connection))
            {
                connection.NotifyClosed();
                ConnectionClosed(this, new ConnectionEventArgs<SignalRConnection>() { Connection = connection });
            }
        }
    }
}
