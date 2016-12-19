using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Guncho.Connections
{
    internal sealed class TcpConnectionManager : IConnectionManager<TcpConnection>
    {
        private readonly TcpListener listener;
        private readonly ConcurrentDictionary<int, Task> activeConnections = new ConcurrentDictionary<int, Task>();

        public TcpConnectionManager(IPAddress address, int port)
        {
            this.listener = new TcpListener(address, port);

            this.ConnectionAccepted = delegate { };
            this.ConnectionClosed = delegate { };
        }

        public event EventHandler<ConnectionAcceptedEventArgs<TcpConnection>> ConnectionAccepted;
        public event EventHandler<ConnectionEventArgs<TcpConnection>> ConnectionClosed;

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            listener.Start();

            try
            {
                int connectionId = 0;

                while (!cancellationToken.IsCancellationRequested)
                {
                    var tcpClient = await listener.AcceptTcpClientAsync().WithCancellation(cancellationToken);

                    // Start a background task to deal with the client.
                    var clientTask = HandleTcpClientAsync(tcpClient, connectionId, cancellationToken);
                    activeConnections[connectionId] = clientTask;
                    connectionId++;
                }

                await Task.WhenAll(activeConnections.Values);
            }
            finally
            {
                listener.Stop();
            }
        }

        private async Task HandleTcpClientAsync(TcpClient tcpClient, int id, CancellationToken cancellationToken)
        {
            try
            {
                var tcpConnection = new TcpConnection(tcpClient);
                ConnectionAccepted?.Invoke(this, new ConnectionAcceptedEventArgs<TcpConnection> { Connection = tcpConnection, AuthenticatedUserName = null });

                try
                {
                    await tcpConnection.WhenClosed();
                }
                finally
                {
                    ConnectionClosed?.Invoke(this, new ConnectionEventArgs<TcpConnection> { Connection = tcpConnection });
                }
            }
            finally
            {
                Task dummy;
                activeConnections.TryRemove(id, out dummy);
            }
        }
    }
}
