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

        public virtual string JoinCommand
        {
            get { return "$join"; }
        }

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
}
