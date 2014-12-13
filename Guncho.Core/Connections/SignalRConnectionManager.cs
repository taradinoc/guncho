using Microsoft.AspNet.SignalR;
using System;
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
        void NotifyConnectionAccepted(string connectionId, string playerName = null);
        void NotifyConnectionClosed(string connectionId);
    }

    internal sealed class SignalRConnectionManager : ISignalRConnectionManager
    {
        private IHubContext hubContext;

        public SignalRConnectionManager()
        {
        }

        public Task Run(CancellationToken cancellationToken)
        {
            hubContext = GlobalHost.ConnectionManager.GetHubContext("PlayHub");

            throw new NotImplementedException();
        }

        public event EventHandler<ConnectionAcceptedEventArgs<SignalRConnection>> ConnectionAccepted;
        public event EventHandler<ConnectionEventArgs<SignalRConnection>> ConnectionClosed;

        public SignalRConnection GetConnectionById(string connectionId)
        {
            throw new NotImplementedException();
        }

        public void NotifyConnectionAccepted(string connectionId, string playerName = null)
        {
            throw new NotImplementedException();
        }

        public void NotifyConnectionClosed(string connectionId)
        {
            throw new NotImplementedException();
        }
    }
}
