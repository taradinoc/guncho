using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace Guncho
{
    abstract class Player
    {
        private string name;
        private GameInstance instance;

        public Player(string name)
        {
            this.name = name;
        }

        public string Name
        {
            get { return name; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException();

                name = value;
            }
        }

        public abstract string LogName { get; }

        public virtual bool IsAdmin
        {
            get { return false; }
        }

        public virtual bool IsGuest
        {
            get { return false; }
        }

        public Realm Realm
        {
            get { return instance.Realm; }
        }

        public GameInstance Instance
        {
            get { return instance; }
            set { instance = value; }
        }

        public virtual string Disambiguating
        {
            get { return null; }
            set { /* nada */ }
        }

        public virtual string GetAttribute(string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            return "";
        }

        public virtual IEnumerable<KeyValuePair<string, string>> GetAllAttributes()
        {
            yield break;
        }

        public virtual void SetAttribute(string name, string value)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            // nada
        }

        public virtual void NotifyInstanceReloading()
        {
            // nada
        }

        public virtual void FlushOutput()
        {
            // nada
        }

        public abstract void Write(char c);
        public abstract void Write(string text);

        public void WriteLine()
        {
            Write("\r\n");
        }

        public void WriteLine(string text)
        {
            Write(text);
            WriteLine();
        }

        public void WriteLine(string format, params object[] args)
        {
            Write(string.Format(format, args));
            WriteLine();
        }
    }

    class NetworkPlayer : Player
    {
        private readonly int id;
        private readonly bool isAdmin, isGuest;
        private readonly Dictionary<string, string> attributes = new Dictionary<string, string>();
        private string dab, again;
        private string pwdSalt, pwdHash;
        private Connection conn;

        public NetworkPlayer(int id, string name, bool isAdmin)
            : this(id, name, isAdmin, false)
        {
        }

        public NetworkPlayer(int id, string name, bool isAdmin, bool isGuest)
            : base(name)
        {
            this.id = id;
            this.isAdmin = isAdmin;
            this.isGuest = isGuest;
        }

        public override string LogName
        {
            get { return string.Format("{0} (#{1})", Name, ID); }
        }

        public int ID
        {
            get { return id; }
        }

        public string PasswordSalt
        {
            get { return pwdSalt; }
            set { pwdSalt = value; }
        }

        public string PasswordHash
        {
            get { return pwdHash; }
            set { pwdHash = value; }
        }

        public override bool IsAdmin
        {
            get { return isAdmin; }
        }

        public override bool IsGuest
        {
            get { return isGuest; }
        }

        public override string Disambiguating
        {
            get { return dab; }
            set { dab = value; }
        }

        public string LastCommand
        {
            get { return again; }
            set { again = value; }
        }

        public Connection Connection
        {
            get { return conn; }
            set { conn = value; }
        }

        public override string GetAttribute(string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            string result;
            if (attributes.TryGetValue(name, out result))
                return result;
            else
                return "";
        }

        public override IEnumerable<KeyValuePair<string, string>> GetAllAttributes()
        {
            return attributes;
        }

        public override void SetAttribute(string name, string value)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (value == null || value.Length == 0)
                attributes.Remove(name);
            else
                attributes[name] = value;
        }

        public override void NotifyInstanceReloading()
        {
            lock (this)
                if (conn != null)
                    conn.WriteLine("[The realm shimmers for a moment...]");
        }

        public override void FlushOutput()
        {
            lock (this)
                if (conn != null)
                    conn.FlushOutput();
        }

        public override void Write(char c)
        {
            lock (this)
                if (conn != null)
                    conn.Write(c);
        }

        public override void Write(string text)
        {
            lock (this)
                if (conn != null)
                    conn.Write(text);
        }
    }

    class DummyPlayer : Player
    {
        public DummyPlayer(string name)
            : base(name)
        {
        }

        public override string LogName
        {
            get { return Name + " (dummy)"; }
        }

        public override void Write(char c)
        {
            // nada
        }

        public override void Write(string text)
        {
            // nada
        }
    }

    class BotPlayer : Player
    {
        private readonly BotInstance instance;
        private readonly StringBuilder buffer = new StringBuilder();
        private readonly StringBuilder lineBuffer = new StringBuilder();

        public BotPlayer(BotInstance instance, string name)
            : base(name)
        {
            this.instance = instance;
        }

        public override string LogName
        {
            get { return Name + " (bot)"; }
        }

        public override void NotifyInstanceReloading()
        {
            instance.ReceiveLine(this, "$reloading");
        }

        public override string GetAttribute(string name)
        {
            return base.GetAttribute(name);
        }

        public override IEnumerable<KeyValuePair<string, string>> GetAllAttributes()
        {
            return base.GetAllAttributes();
        }

        public override void Write(char c)
        {
            buffer.Append(c);
            if (c == '\n')
                CheckForLines();
        }

        public override void Write(string text)
        {
            buffer.Append(text);
            CheckForLines();
        }

        private void CheckForLines()
        {
            while (buffer.Length > 0)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i] == '\n')
                    {
                        // all the lines we care about start with a dollar sign
                        if (buffer[0] == '$')
                        {
                            for (int j = 0; j < i; j++)
                                lineBuffer.Append(buffer[j]);

                            if (lineBuffer.Length > 0 && lineBuffer[lineBuffer.Length - 1] == '\r')
                                lineBuffer.Remove(lineBuffer.Length - 1, 1);

                            if (lineBuffer.Length > 0)
                                instance.ReceiveLine(this, lineBuffer.ToString());

                            lineBuffer.Length = 0;
                        }

                        buffer.Remove(0, i + 1);
                        break;
                    }
                }
            }
        }
    }
}
