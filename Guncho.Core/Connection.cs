using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace Guncho
{
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

        public abstract string ReadLine();

        public abstract void Write(string text);

        public abstract void Write(char c);

        public virtual void WriteLine(string text)
        {
            Write(text);
            WriteLine();
        }

        public virtual void WriteLine(string format, params object[] args)
        {
            WriteLine(string.Format(format, args));
        }

        public virtual void WriteLine()
        {
            WriteLine("");
        }

        public abstract void Terminate(bool wait);

        public abstract void FlushOutput();
    }

    public sealed class TcpConnection : Connection
    {
        private readonly TcpClient client;
        private readonly StreamReader rdr;
        private readonly StreamWriter wtr;
        private readonly StringBuilder outputBuffer = new StringBuilder();
        private readonly Thread clientThread;

        public TcpConnection(TcpClient client)
        {
            this.client = client;

            NetworkStream stream = client.GetStream();
            this.rdr = new StreamReader(stream);
            this.wtr = new StreamWriter(stream);

            this.clientThread = Thread.CurrentThread;
        }

        /// <summary>
        /// Read a line of input from the connection, blocking if a line is
        /// not yet available.
        /// </summary>
        /// <returns>The line of input, or <b>null</b> if the connection was
        /// closed.</returns>
        public override string ReadLine()
        {
            try
            {
                FlushOutput();

                string str = rdr.ReadLine();
                LastActivity = DateTime.Now;
                return str;
            }
            catch (IOException)
            {
                return null;
            }
        }

        public override void Write(string text)
        {
            outputBuffer.Append(text);
        }

        public override void Write(char c)
        {
            outputBuffer.Append(c);
        }

        public override void WriteLine(string text)
        {
            outputBuffer.AppendLine(text);
        }

        public override void WriteLine(string format, params object[] args)
        {
            outputBuffer.AppendFormat(format, args);
            outputBuffer.AppendLine();
        }

        public override void WriteLine()
        {
            outputBuffer.AppendLine();
        }

        public override void Terminate(bool wait)
        {
            FlushOutput();
            client.Client.Shutdown(SocketShutdown.Both);
            client.Client.Close();

            if (wait && Thread.CurrentThread != clientThread)
            {
                clientThread.Join();
            }
        }

        public override void FlushOutput()
        {
            // trim leading and trailing newlines
            string line = outputBuffer.ToString().Trim(new char[] { '\r', '\n' });
            if (line.Length > 0)
            {
                string rawLine = Server.Desanitize(line);
                if (rawLine.EndsWith("\n>"))
                    wtr.Write(rawLine);
                else
                    wtr.WriteLine(rawLine);
            }
            outputBuffer.Length = 0;
            wtr.Flush();
        }
    }
}
