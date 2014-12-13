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
        private readonly string connectionId;

        private readonly BufferBlock<string> commandQueue = new BufferBlock<string>();  // TODO: capacity limiting?

        public SignalRConnection(ISignalRConnectionManager manager, string connectionId)
        {
            this.manager = manager;
            this.connectionId = connectionId;
        }

        public override Task WhenClosed()
        {
            throw new NotImplementedException();
        }

        public override string ReadLine()
        {
            throw new NotImplementedException();
        }

        public async override Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            return await commandQueue.ReceiveAsync(cancellationToken);
        }

        public override void Write(char c)
        {
            throw new NotImplementedException();
        }

        public override void Write(string text)
        {
            throw new NotImplementedException();
        }

        public override Task WriteAsync(char c)
        {
            throw new NotImplementedException();
        }

        public override Task WriteAsync(string text)
        {
            throw new NotImplementedException();
        }

        public override void WriteLine(string text)
        {
            throw new NotImplementedException();
        }

        public override Task WriteLineAsync(string text)
        {
            throw new NotImplementedException();
        }

        public override void Terminate(bool wait)
        {
            throw new NotImplementedException();
        }

        public override void FlushOutput()
        {
            throw new NotImplementedException();
        }

        public override Task FlushOutputAsync()
        {
            throw new NotImplementedException();
        }

        internal void EnqueueCommand(string command)
        {
            commandQueue.Post(command);
        }
    }
}
