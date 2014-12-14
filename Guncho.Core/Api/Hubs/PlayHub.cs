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
        Task WriteLine(string line);

        Task Goodbye();
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
            System.Diagnostics.Debug.WriteLine("OnConnected: id = {0}", new string[] { Context.ConnectionId });

            string userName = null;
            if (Context.User != null && Context.User.Identity != null)
            {
                userName = Context.User.Identity.Name;
            }

            manager.NotifyConnectionAccepted(Context.ConnectionId, userName);
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            System.Diagnostics.Debug.WriteLine("OnDisconnected: id = {0}, stopCalled = {1}", new object[] { Context.ConnectionId, stopCalled });
            manager.NotifyConnectionClosed(Context.ConnectionId);
            return base.OnDisconnected(stopCalled);
        }

        public Task SendCommand(string command)
        {
            System.Diagnostics.Debug.WriteLine("SendCommand: id = {0}", new string[] { Context.ConnectionId });
            var connection = manager.GetConnectionById(Context.ConnectionId);
            connection.EnqueueCommand(command);
            return Task.FromResult(0);
        }
    }
}
