using Nito.AsyncEx;
using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Guncho.Connections
{
    public sealed class TcpConnection : Connection
    {
        private readonly TcpClient client;
        private readonly StreamReader rdr;
        private readonly StreamWriter wtr;
        private readonly StringBuilder outputBuffer = new StringBuilder();
        private readonly TaskCompletionSource<bool> whenClosed = new TaskCompletionSource<bool>();
        private bool _skipNextLF;

        public TcpConnection(TcpClient client)
        {
            ArgumentNullException.ThrowIfNull(client);

            this.client = client;
            this.OtherSide = client.Client.RemoteEndPoint;

            NetworkStream stream = client.GetStream();
            this.rdr = new StreamReader(stream);
            this.wtr = new StreamWriter(stream);
        }

        public EndPoint? OtherSide { get; private set; }

        public override Task WhenClosed()
        {
            return whenClosed.Task;
        }


        /// <summary>
        /// Read a line of input from the connection, blocking if a line is
        /// not yet available.
        /// </summary>
        /// <returns>The line of input, or <b>null</b> if the connection was
        /// closed.</returns>
        public async override Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            try
            {
                var sb = new StringBuilder();

                // If the previous line terminated with CR and the next char is LF, skip it
                if (_skipNextLF && rdr.Peek() == '\n')
                {
                    // Consume the LF
                    var discard = new char[1];
                    await rdr.ReadAsync(discard, 0, 1);
                    _skipNextLF = false;
                }

                var buffer = new char[1];
                while (true)
                {
                    int read = await rdr.ReadAsync(buffer, 0, 1);
                    if (read == 0)
                    {
                        whenClosed.TrySetResult(true);
                        return null!;
                    }

                    char ch = buffer[0];
                    if (ch == '\r')
                    {
                        // End of line; mark to swallow a following LF if present
                        _skipNextLF = true;
                        break;
                    }
                    else if (ch == '\n')
                    {
                        // End of line
                        _skipNextLF = false;
                        break;
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }

                LastActivity = DateTime.Now;
                return sb.ToString();
            }
            catch (IOException)
            {
                whenClosed.TrySetResult(true);
                return null!;
            }
        }

        public override Task WriteAsync(string text)
        {
            outputBuffer.Append(text);
            return Task.CompletedTask;
        }
        
        public override Task WriteAsync(char c)
        {
            outputBuffer.Append(c);
            return Task.CompletedTask;
        }

        public override Task WriteLineAsync(string text)
        {
            outputBuffer.AppendLine(text);
            return Task.CompletedTask;
        }

        public override async Task TerminateAsync()
        {
            // Flush any pending output first.
            await FlushOutputAsync();
            // Mark closed immediately to unblock waiting tasks (quit path race avoidance).
            whenClosed.TrySetResult(true);
            try
            {
                client.Client.Shutdown(SocketShutdown.Both);
            }
            catch { /* ignore if already closed */ }
            client.Close();
        }

        public async override Task FlushOutputAsync()
        {
            if (outputBuffer.Length == 0)
            {
                return;
            }

            string rawText = TextUtils.Desanitize(outputBuffer.ToString());
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

                    await wtr.WriteLineAsync(string.Empty);
                    LastLineWasBlank = true;
                    return;
                }

                await wtr.WriteLineAsync(line);
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
                var remainder = lineBuffer.ToString();
                lineBuffer.Clear();
                await wtr.WriteAsync(remainder);
                LastLineWasBlank = false;
            }

            await wtr.FlushAsync();
        }
    }
}
