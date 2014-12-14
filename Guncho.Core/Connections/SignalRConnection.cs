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

        public override void Write(char c)
        {
            outputBuffer.Append(c);
        }

        public override void Write(string text)
        {
            outputBuffer.Append(text);
        }

        public override Task WriteAsync(char c)
        {
            outputBuffer.Append(c);
            return Task.FromResult(0);
        }

        public override Task WriteAsync(string text)
        {
            outputBuffer.Append(text);
            return Task.FromResult(0);
        }

        public override void WriteLine(string text)
        {
            outputBuffer.AppendLine(text);
        }

        public override Task WriteLineAsync(string text)
        {
            outputBuffer.AppendLine(text);
            return Task.FromResult(0);
        }

        public override void Terminate(bool wait)
        {
            FlushOutput();

            var task = manager.TerminateClientAsync(ConnectionId);
            if (wait)
            {
                task.Wait();
            }
        }

        public override void FlushOutput()
        {
            FlushOutputAsync().Wait();
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
