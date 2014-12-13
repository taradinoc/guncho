using Guncho.Connections;
using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho.Api.Hubs
{
    public interface IClient
    {
        void WriteLine(string line);
    }

    public sealed class PlayHub : Hub<IClient>
    {
        private readonly ISignalRConnectionManager manager;

        public PlayHub(ISignalRConnectionManager manager)
        {
            this.manager = manager;
        }

        public override Task OnConnected()
        {
            manager.NotifyConnectionAccepted(Context.ConnectionId, Context.User.Identity.Name);
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            manager.NotifyConnectionClosed(Context.ConnectionId);
            return base.OnDisconnected(stopCalled);
        }

        public Task SendCommand(string command)
        {
            var connection = manager.GetConnectionById(Context.ConnectionId);
            connection.EnqueueCommand(command);
            return Task.FromResult(0);
        }
    }
}
