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
    class TcpConnectionEventArgs : EventArgs
    {
        public TcpConnection Connection { get; set; }
    }

    internal sealed class TcpConnectionManager
    {
        private readonly TcpListener listener;
        private readonly ConcurrentDictionary<int, Task> activeConnections = new ConcurrentDictionary<int, Task>();

        public TcpConnectionManager(IPAddress address, int port)
        {
            this.listener = new TcpListener(address, port);

            this.ConnectionAccepted = delegate { };
            this.ConnectionClosed = delegate { };
        }

        public event EventHandler<TcpConnectionEventArgs> ConnectionAccepted;
        public event EventHandler<TcpConnectionEventArgs> ConnectionClosed;

        public async Task Run(CancellationToken cancellationToken)
        {
            listener.Start();

            try
            {
                int connectionId = 0;

                while (!cancellationToken.IsCancellationRequested)
                {
                    var tcpClient = await listener.AcceptTcpClientAsync().WithCancellation(cancellationToken);

                    // Start a background task to deal with the client.
                    var clientTask = HandleTcpClient(tcpClient, connectionId, cancellationToken);
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

        private async Task HandleTcpClient(TcpClient tcpClient, int id, CancellationToken cancellationToken)
        {
            try
            {
                var tcpConnection = new TcpConnection(tcpClient);
                ConnectionAccepted(this, new TcpConnectionEventArgs { Connection = tcpConnection });

                try
                {
                    await tcpConnection.WhenClosed();
                }
                finally
                {
                    ConnectionClosed(this, new TcpConnectionEventArgs { Connection = tcpConnection });
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
