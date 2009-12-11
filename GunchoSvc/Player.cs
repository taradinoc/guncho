using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace Guncho
{
    class Player
    {
        private readonly int id;
        private readonly bool isAdmin, isGuest;
        private readonly Dictionary<string, string> attributes = new Dictionary<string, string>();
        private string name, dab, again;
        private string pwdSalt, pwdHash;
        private Connection conn;
        private Instance instance;

        public Player(int id, string name, bool isAdmin)
            : this(id, name, isAdmin, false)
        {
        }

        public Player(int id, string name, bool isAdmin, bool isGuest)
        {
            this.id = id;
            this.name = name;
            this.isAdmin = isAdmin;
            this.isGuest = isGuest;
        }

        public int ID
        {
            get { return id; }
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

        public bool IsAdmin
        {
            get { return isAdmin; }
        }

        public bool IsGuest
        {
            get { return isGuest; }
        }

        public string Disambiguating
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

        public Realm Realm
        {
            get { return instance.Realm; }
        }

        public Instance Instance
        {
            get { return instance; }
            set { instance = value; }
        }

        public string GetAttribute(string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            string result;
            if (attributes.TryGetValue(name, out result))
                return result;
            else
                return "";
        }

        public IEnumerable<KeyValuePair<string, string>> GetAllAttributes()
        {
            return attributes;
        }

        public void SetAttribute(string name, string value)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (value == null || value.Length == 0)
                attributes.Remove(name);
            else
                attributes[name] = value;
        }
    }
}
