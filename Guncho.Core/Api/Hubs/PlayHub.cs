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
        public void CreateSession()
        {
            Clients.Caller.WriteLine("Hello from Guncho via SignalR!");
        }

        public Task SendCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                Clients.Caller.WriteLine("I beg your pardon?");
            }
            else
            {
                Clients.Caller.WriteLine("But why should I " + command.ToUpper() + "?");
            }
            return Task.FromResult(0);
        }
    }
}
