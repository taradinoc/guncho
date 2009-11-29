using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace Guncho
{
    class Connection
    {
        private readonly TcpClient client;
        private readonly StreamReader rdr;
        private readonly StreamWriter wtr;
        private readonly DateTime started;
        private readonly StringBuilder outputBuffer = new StringBuilder();
        private readonly Thread clientThread;
        private DateTime lastActivity;
        private Player player;

        public Connection(TcpClient client)
        {
            this.client = client;

            NetworkStream stream = client.GetStream();
            this.rdr = new StreamReader(stream);
            this.wtr = new StreamWriter(stream);

            this.started = this.lastActivity = DateTime.Now;
            this.clientThread = Thread.CurrentThread;
        }

        public Player Player
        {
            get { return player; }
            set { player = value; }
        }

        public TimeSpan ConnectedTime
        {
            get { return DateTime.Now - started; }
        }

        public TimeSpan IdleTime
        {
            get { return DateTime.Now - lastActivity; }
        }

        public Thread ClientThread
        {
            get { return clientThread; }
        }

        /// <summary>
        /// Read a line of input from the connection, blocking if a line is
        /// not yet available.
        /// </summary>
        /// <returns>The line of input, or <b>null</b> if the connection was
        /// closed.</returns>
        public string ReadLine()
        {
            try
            {
                FlushOutput();

                string str = rdr.ReadLine();
                lastActivity = DateTime.Now;
                return str;
            }
            catch (IOException)
            {
                return null;
            }
        }

        public void Write(string text)
        {
            outputBuffer.Append(text);
        }

        public void Write(char c)
        {
            outputBuffer.Append(c);
        }

        public void WriteLine(string format, params object[] args)
        {
            outputBuffer.AppendFormat(format, args);
            outputBuffer.AppendLine();
        }

        public void WriteLine(string text)
        {
            outputBuffer.AppendLine(text);
        }

        public void WriteLine()
        {
            outputBuffer.AppendLine();
        }

        public void Terminate()
        {
            FlushOutput();
            client.Client.Shutdown(SocketShutdown.Both);
            client.Client.Close();
        }

        public void FlushOutput()
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
