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

        public override async Task FlushOutputAsync()
        {
            if (outputBuffer.Length == 0)
                return;

            var rawText = TextUtils.Desanitize(outputBuffer.ToString());
            outputBuffer.Length = 0;

            var lineBuffer = new StringBuilder();
            async Task EmitLineAsync()
            {
                var line = lineBuffer.ToString();
                lineBuffer.Clear();

                if (line.Length == 0)
                {
                    if (FilterBlankLines)
                        return;

                    if (LastLineWasBlank)
                        return;

                    await manager.SendToClientAsync(ConnectionId, string.Empty);
                    LastLineWasBlank = true;
                    return;
                }

                await manager.SendToClientAsync(ConnectionId, line);
                LastLineWasBlank = false;
            }

            foreach (var ch in rawText)
            {
                if (ch == '\r')
                {
                    continue;
                }

                if (ch == '\n')
                {
                    await EmitLineAsync();
                }
                else
                {
                    lineBuffer.Append(ch);
                }
            }

            if (lineBuffer.Length > 0)
            {
                await manager.SendToClientAsync(ConnectionId, lineBuffer.ToString());
                lineBuffer.Clear();
                LastLineWasBlank = false;
            }
        }

        internal void EnqueueCommand(string command)
        {
            commandQueue.Writer.TryWrite(command);
        }
    }
}
