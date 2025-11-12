using Guncho.Connections;
using Guncho.WebHost.Services;
using System.Text;
using System.Threading.Channels;

namespace Guncho.WebHost.Connections
{
    public sealed class SignalRConnection : Connection
    {
        private readonly ISignalRConnectionManager manager;
        private readonly Channel<string> commandQueue = Channel.CreateUnbounded<string>();
        private readonly StringBuilder outputBuffer = new();
        private readonly TaskCompletionSource whenClosed = new();

        public SignalRConnection(ISignalRConnectionManager manager, string connectionId)
        {
            this.manager = manager;
            this.ConnectionId = connectionId;
        }

        public string ConnectionId { get; private set; }

        public void NotifyClosed()
        {
            whenClosed.TrySetResult();
        }

        public override Task WhenClosed()
        {
            return whenClosed.Task;
        }

        public override async Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            await FlushOutputAsync();
            
            cancellationToken.ThrowIfCancellationRequested();

            var readTask = commandQueue.Reader.ReadAsync(cancellationToken).AsTask();
            var closedTask = WhenClosed();
            
            var completed = await Task.WhenAny(readTask, closedTask);
            if (completed == readTask)
            {
                LastActivity = DateTime.Now;
                return await readTask;
            }
            else
            {
                return null!;
            }
        }

        public override Task WriteAsync(char c)
        {
            outputBuffer.Append(c);
            return Task.CompletedTask;
        }

        public override Task WriteAsync(string text)
        {
            outputBuffer.Append(text);
            return Task.CompletedTask;
        }

        public override Task WriteLineAsync(string text)
        {
            outputBuffer.AppendLine(text);
            return Task.CompletedTask;
        }

        public override async Task TerminateAsync()
        {
            await FlushOutputAsync();
            await manager.TerminateClientAsync(ConnectionId);
        }

        private static readonly char[] LineDelimiters = { '\r', '\n' };

        public override async Task FlushOutputAsync()
        {
            var bufferContent = outputBuffer.ToString();
            var lines = bufferContent.Split(LineDelimiters, StringSplitOptions.RemoveEmptyEntries);
            outputBuffer.Length = 0;

            foreach (var line in lines)
            {
                string rawLine = TextUtils.Desanitize(line);
                await manager.SendToClientAsync(ConnectionId, rawLine);
            }
        }

        internal void EnqueueCommand(string command)
        {
            commandQueue.Writer.TryWrite(command);
        }
    }
}
