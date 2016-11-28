using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Guncho.Connections
{
    public sealed class SignalRConnection : Connection
    {
        private readonly ISignalRConnectionManager manager;
        private readonly BufferBlock<string> commandQueue = new BufferBlock<string>();  // TODO: capacity limiting?
        private readonly StringBuilder outputBuffer = new StringBuilder();
        private readonly TaskCompletionSource<bool> whenClosed = new TaskCompletionSource<bool>();

        public SignalRConnection(ISignalRConnectionManager manager, string connectionId)
        {
            this.manager = manager;
            this.ConnectionId = connectionId;
        }

        public string ConnectionId { get; private set; }

        public void NotifyClosed()
        {
            whenClosed.TrySetResult(true);
        }

        public override Task WhenClosed()
        {
            return whenClosed.Task;
        }

        public async override Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            await FlushOutputAsync();
            
            cancellationToken.ThrowIfCancellationRequested();

            var receiveTask = commandQueue.ReceiveAsync(cancellationToken);
            if (await Task.WhenAny(receiveTask, WhenClosed()) == receiveTask)
            {
                LastActivity = DateTime.Now;
                return receiveTask.Result;
            }
            else
            {
                return null;
            }
        }

        public override Task WriteAsync(char c)
        {
            outputBuffer.Append(c);
            return TaskConstants.Completed;
        }

        public override Task WriteAsync(string text)
        {
            outputBuffer.Append(text);
            return TaskConstants.Completed;
        }

        public override Task WriteLineAsync(string text)
        {
            outputBuffer.AppendLine(text);
            return TaskConstants.Completed;
        }

        public override async Task TerminateAsync()
        {
            await FlushOutputAsync();
            await manager.TerminateClientAsync(ConnectionId);
        }

        private static readonly char[] LineDelimiters = { '\r', '\n' };

        public async override Task FlushOutputAsync()
        {
            var lines = outputBuffer.ToString().Split(LineDelimiters, StringSplitOptions.RemoveEmptyEntries);
            outputBuffer.Length = 0;

            foreach (var line in lines)
            {
                string rawLine = Server.Desanitize(line);
                await manager.SendToClientAsync(ConnectionId, rawLine);
            }
        }

        internal void EnqueueCommand(string command)
        {
            commandQueue.Post(command);
        }
    }
}
