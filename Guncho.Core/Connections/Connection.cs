using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using Nito.AsyncEx;

namespace Guncho.Connections
{
    [ContractClass(typeof(ConnectionContract))]
    public abstract class Connection
    {
        public Connection()
        {
            this.Started = this.LastActivity = DateTime.Now;
        }

        public AsyncReaderWriterLock Lock { get; } = new AsyncReaderWriterLock();
        public Player Player { get; set; }
        public DateTime Started { get; set; }
        public DateTime LastActivity { get; set; }

        public TimeSpan ConnectedTime => DateTime.Now - Started;
        public TimeSpan IdleTime => DateTime.Now - LastActivity;

        public abstract Task WhenClosed();

        public abstract Task<string> ReadLineAsync(CancellationToken cancellationToken);

        public abstract Task WriteAsync(char c);

        public abstract Task WriteAsync(string text);

        public async virtual Task WriteLineAsync()
        {
            Contract.Ensures(Contract.Result<Task>() != null);

            await WriteLineAsync("");
        }

        public abstract Task WriteLineAsync(string text);

        public async virtual Task WriteLineAsync(string format, params object[] args)
        {
            Contract.Requires(format != null);
            Contract.Requires(args != null);
            Contract.Ensures(Contract.Result<Task>() != null);

            await WriteLineAsync(string.Format(format, args));
        }

        public abstract Task TerminateAsync();

        public abstract Task FlushOutputAsync();
    }

    [ContractClassFor(typeof(Connection))]
    internal abstract class ConnectionContract : Connection
    {
        public override Task WhenClosed()
        {
            Contract.Ensures(Contract.Result<Task>() != null);
            return default(Task);
        }

        public override Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            Contract.Ensures(Contract.Result<Task<string>>() != null);
            return default(Task<string>);
        }

        public override Task WriteAsync(char c)
        {
            Contract.Ensures(Contract.Result<Task>() != null);
            return default(Task);
        }

        public override Task WriteAsync(string text)
        {
            Contract.Requires(text != null);
            Contract.Ensures(Contract.Result<Task>() != null);
            return default(Task);
        }

        public override Task WriteLineAsync(string text)
        {
            Contract.Requires(text != null);
            Contract.Ensures(Contract.Result<Task>() != null);
            return default(Task);
        }

        public override Task TerminateAsync()
        {
            Contract.Ensures(Contract.Result<Task>() != null);
            return default(Task);
        }

        public override Task FlushOutputAsync()
        {
            Contract.Ensures(Contract.Result<Task>() != null);
            return default(Task);
        }
    }
}
