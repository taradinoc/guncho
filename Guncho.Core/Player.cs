using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using Guncho.Connections;
using Nito.AsyncEx;

namespace Guncho
{
    public class Player
    {
        private readonly Dictionary<string, string> attributes = new Dictionary<string, string>();
        private string name;

        public Player(int id, string name, bool isAdmin)
            : this(id, name, isAdmin, false)
        {
        }

        public Player(int id, string name, bool isAdmin, bool isGuest)
        {
            ID = id;
            this.name = name;
            IsAdmin = isAdmin;
            IsGuest = isGuest;
        }

        public AsyncReaderWriterLock Lock { get; } = new AsyncReaderWriterLock();
        public int ID { get; }
        public string PasswordSalt { get; set; }
        public string PasswordHash { get; set; }
        public bool IsAdmin { get; }
        public bool IsGuest { get; }
        public string Disambiguating { get; set; }
        public string LastCommand { get; set; }
        public Connection Connection { get; set; }
        public Realm Realm => Instance?.Realm;
        public Instance Instance { get; set; }

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
