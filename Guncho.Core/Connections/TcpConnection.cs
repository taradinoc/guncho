using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
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

        public TcpConnection(TcpClient client)
        {
            Contract.Requires(client != null);

            this.client = client;
            this.OtherSide = client.Client.RemoteEndPoint;

            NetworkStream stream = client.GetStream();
            this.rdr = new StreamReader(stream);
            this.wtr = new StreamWriter(stream);
        }

        public EndPoint OtherSide { get; private set; }

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
                await FlushOutputAsync();
                var str = await rdr.ReadLineAsync().WithCancellation(cancellationToken);
                LastActivity = DateTime.Now;
                return str;
            }
            catch (IOException)
            {
                whenClosed.TrySetResult(true);
                return null;
            }
        }

        public override Task WriteAsync(string text)
        {
            outputBuffer.Append(text);
            return TaskConstants.Completed;
        }
        
        public override Task WriteAsync(char c)
        {
            outputBuffer.Append(c);
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
            client.Client.Shutdown(SocketShutdown.Both);
            client.Client.Close();

            await whenClosed.Task;
        }

        public async override Task FlushOutputAsync()
        {
            // trim leading and trailing newlines
            string line = outputBuffer.ToString().Trim(new char[] { '\r', '\n' });
            if (line.Length > 0)
            {
                string rawLine = Server.Desanitize(line);
                if (rawLine.EndsWith("\n>"))
                    await wtr.WriteAsync(rawLine);
                else
                    await wtr.WriteLineAsync(rawLine);
            }
            outputBuffer.Length = 0;
            await wtr.FlushAsync();
        }
    }
}
