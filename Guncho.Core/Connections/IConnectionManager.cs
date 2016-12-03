using System;
using System.Threading;
using System.Threading.Tasks;

namespace Guncho.Connections
{
    public class ConnectionEventArgs<TConnection> : EventArgs where TConnection : Connection
    {
        public TConnection Connection { get; set; }
    }
    
    public class ConnectionAcceptedEventArgs<TConnection> : ConnectionEventArgs<TConnection> where TConnection : Connection
    {
        /// <summary>
        /// The name of the user if the user's identity is already authenticated, otherwise null.
        /// </summary>
        public string AuthenticatedUserName { get; set; }
    }

    public interface IConnectionManager<TConnection> where TConnection : Connection
    {
        event EventHandler<ConnectionAcceptedEventArgs<TConnection>> ConnectionAccepted;
        event EventHandler<ConnectionEventArgs<TConnection>> ConnectionClosed;
        Task RunAsync(CancellationToken cancellationToken);
    }
}
