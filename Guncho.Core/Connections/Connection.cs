using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;

namespace Guncho.Connections
{
    [ContractClass(typeof(ConnectionContract))]
    public abstract class Connection
    {
        public Connection()
        {
            this.Started = this.LastActivity = DateTime.Now;
        }

        public Player Player { get; set; }
        public DateTime Started { get; set; }
        public DateTime LastActivity { get; set; }

        public TimeSpan ConnectedTime
        {
            get { return DateTime.Now - Started; }
        }

        public TimeSpan IdleTime
        {
            get { return DateTime.Now - LastActivity; }
        }

        public abstract Task WhenClosed();

        [Obsolete]
        public abstract string ReadLine();

        public abstract Task<string> ReadLineAsync(CancellationToken cancellationToken);

        public abstract void Write(char c);

        public abstract void Write(string text);

        public abstract Task WriteAsync(char c);

        public abstract Task WriteAsync(string text);

        public virtual void WriteLine()
        {
            WriteLine("");
        }

        public abstract void WriteLine(string text);

        public virtual void WriteLine(string format, params object[] args)
        {
            Contract.Requires(format != null);
            Contract.Requires(args != null);

            WriteLine(string.Format(format, args));
        }

        public async virtual Task WriteLineAsync()
        {
            await WriteLineAsync("");
        }

        public abstract Task WriteLineAsync(string text);

        public async virtual Task WriteLineAsync(string format, params object[] args)
        {
            await WriteLineAsync(string.Format(format, args));
        }

        public abstract void Terminate(bool wait);

        public abstract void FlushOutput();

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

        public override void Write(string text)
        {
            Contract.Requires(text != null);
        }
    }
}
