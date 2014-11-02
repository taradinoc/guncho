using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;
using System.IO;
using System.Security.Cryptography;

namespace Guncho
{
    public interface IController
    {
        bool CreatePlayer(string name, string password);
        bool LogIn(string name, string password);
        string CapitalizePlayerName(string name);
        bool IsPlayer(string name);
        bool IsPlayerAdmin(string name);
        string GetPlayerAttribute(string playerName, string attrName);
        void SetPlayerAttribute(string playerName, string attrName, string newValue);
        void ChangePassword(string playerName, string newPassword);
        void SavePlayerChanges(string playerName);

        string[] ListRealms();
        string[] ListRealmFactories();
        bool CreateRealm(string playerName, string realmName, string factoryName);
        bool DeleteRealm(string playerName, string realmName);
        string GetRealmSource(string name, out string factoryName);
        RealmEditingOutcome SetRealmSource(string playerName, string realmName, string factoryName, string text);
        string GetRealmIndexPath(string name);
        string GetRealmOwner(string name);
        bool IsRealmCondemned(string realmName);
        RealmAccessLevel GetAccessLevel(string playerName, string realmName);
        RealmPrivacyLevel GetRealmPrivacy(string realmName);
        bool SetRealmPrivacy(string playerName, string realmName, RealmPrivacyLevel level);
        void GetRealmAccessList(string realmName, out string[] aclNames, out RealmAccessLevel[] aclLevels);
        bool SetRealmAccessList(string playerName, string realmName, string[] aclNames, RealmAccessLevel[] aclLevels);

        string[][] GetWhoList();

        void Shutdown(string reason);
    }

    public class ControllerFactory : MarshalByRefObject
    {
        private static TcpChannel channel;
        private static Server registeredServer;

        private const string FACTORY_URI = "ControllerFactory";

        public ControllerFactory()
        {
            if (registeredServer == null)
                throw new InvalidOperationException("No server has been registered");
        }

        public override object InitializeLifetimeService()
        {
            // live forever
            return null;
        }

        internal static void Register(Server server)
        {
            if (server == null)
                throw new ArgumentNullException("server");

            if (registeredServer != null)
                throw new InvalidOperationException("A server has already been registered");

            registeredServer = server;

            if (channel == null)
            {
                System.Collections.IDictionary props = new System.Collections.Hashtable();
                //props["name"] = channelName;
                props["port"] = Properties.Settings.Default.ControllerPort;
                props["bindTo"] = "127.0.0.1";
                props["typeFilterLevel"] = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;
                BinaryServerFormatterSinkProvider serverProvider = new BinaryServerFormatterSinkProvider();
                serverProvider.TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;

                channel = new TcpChannel(props, null, serverProvider);
                ChannelServices.RegisterChannel(channel, false);
            }

            RemotingConfiguration.RegisterWellKnownServiceType(
                typeof(ControllerFactory),
                FACTORY_URI,
                WellKnownObjectMode.Singleton);
            server.LogMessage(LogLevel.Verbose, "Remote control URI is {0}", channel.GetUrlsForUri(FACTORY_URI)[0]);
        }

        public IController GetController()
        {
            if (registeredServer == null)
                throw new InvalidOperationException("Factory was created incorrectly");

            return new Controller(registeredServer);
        }
    }

    /// <summary>
    /// Exposes server functionality to the web interface, and provides static
    /// utility methods that are used by the server and the web interface.
    /// </summary>
    public class Controller : MarshalByRefObject, IController
    {
        #region Static methods for password hashing

        private const int SALT_LENGTH = 3;

        public static string GenerateSalt()
        {
            const string possibleSalt =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ" +
                "`1234567890-=~!@#$%^*()_+[]\\{}|;:,./?";

            string newSalt = "";
            Random rng = new Random();
            for (int i = 0; i < 3; i++)
                newSalt += possibleSalt[rng.Next(possibleSalt.Length)];
            return newSalt;
        }

        public static string HashPassword(string salt, string password)
        {
            List<byte> bytes = new List<byte>();
            if (salt != null)
                bytes.AddRange(Encoding.UTF8.GetBytes(salt));
            if (password != null)
                bytes.AddRange(Encoding.UTF8.GetBytes(password));

            SHA1 sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(bytes.ToArray());
            return Convert.ToBase64String(hash);
        }

        #endregion

        #region Static methods for sanitizing text

        public static string Sanitize(string str)
        {
            return Server.Sanitize(str);
        }

        public static string Desanitize(string str)
        {
            return Server.Desanitize(str);
        }

        #endregion

        private readonly Server server;

        internal Controller(Server server)
        {
            this.server = server;
        }

        public bool LogIn(string name, string password)
        {
            string salt = server.GetPasswordSalt(name);
            if (salt == null)
                return false;

            Player newPlayer = server.ValidateLogIn(name, salt, HashPassword(salt, password));
            if (newPlayer == null)
            {
                server.LogMessage(LogLevel.Notice, "CP: Failed login attempt for {0}.", name);
                return false;
            }

            server.LogMessage(LogLevel.Verbose, "CP: {0} logged in.", newPlayer.Name);
            return true;
        }

        public string CapitalizePlayerName(string name)
        {
            Player player = server.FindPlayer(name);
            if (player == null)
                return name;
            else
                return player.Name;
        }

        public bool IsPlayer(string name)
        {
            return server.FindPlayer(name) != null;
        }

        public bool IsPlayerAdmin(string name)
        {
            Player player = server.FindPlayer(name);
            if (player == null)
                return false;
            else
                return player.IsAdmin;
        }

        public bool CreatePlayer(string name, string password)
        {
            string salt = GenerateSalt();
            string hash = HashPassword(salt, password);

            Player player = server.CreatePlayer(name, salt, hash);
            return player != null;
        }

        public string GetPlayerAttribute(string playerName, string attrName)
        {
            Player player = server.FindPlayer(playerName);
            if (player == null)
                return "";
            
            lock (player)
                return player.GetAttribute(attrName);
        }

        public void SetPlayerAttribute(string playerName, string attrName, string newValue)
        {
            Player player = server.FindPlayer(playerName);
            if (player != null)
            {
                lock (player)
                    player.SetAttribute(attrName, newValue);
            }
        }

        public void ChangePassword(string playerName, string newPassword)
        {
            Player player = server.FindPlayer(playerName);
            if (player != null)
            {
                string newSalt = GenerateSalt();
                string newHash = HashPassword(newSalt, newPassword);

                lock (player)
                {
                    player.PasswordSalt = newSalt;
                    player.PasswordHash = newHash;
                }
            }
        }

        public void SavePlayerChanges(string playerName)
        {
            server.SavePlayers();
        }

        public string[] ListRealms()
        {
            return server.ListRealms();
        }

        public string[] ListRealmFactories()
        {
            return server.ListRealmFactories();
        }

        public bool CreateRealm(string playerName, string realmName, string factoryName)
        {
            Player player = server.FindPlayer(playerName);
            if (player == null)
                return false;
            else
                return server.CreateRealm(player, realmName, factoryName) != null;
        }

        public bool DeleteRealm(string playerName, string realmName)
        {
            Player player = server.FindPlayer(playerName);
            if (playerName == null)
                return false;

            Realm realm = server.GetRealm(realmName);
            if (realm == null)
                return false;

            if (realm.GetAccessLevel(player) < RealmAccessLevel.SafetyOff)
                return false;

            return server.DeleteRealm(realm);
        }

        public string GetRealmSource(string name, out string factoryName)
        {
            Realm realm = server.GetRealm(name);

            if (realm == null)
                throw new ArgumentException("No such realm", "name");

            server.LogMessage(LogLevel.Spam, "CP: getting source of '{0}'.", name);
            factoryName = realm.Factory.Name;
            return File.ReadAllText(realm.SourceFile);
        }

        public RealmEditingOutcome SetRealmSource(string playerName, string realmName, string factoryName,
            string text)
        {
            Realm oldRealm = server.GetRealm(realmName);
            if (oldRealm == null)
                return RealmEditingOutcome.Missing;

            bool changingFactory = (oldRealm.Factory.Name != factoryName);

            // only admins can edit a condemned realm
            if (oldRealm.IsCondemned)
            {
                Player player = server.FindPlayer(playerName);
                if (player == null || !player.IsAdmin)
                    return RealmEditingOutcome.PermissionDenied;
            }

            if (GetAccessLevel(playerName, realmName) >= RealmAccessLevel.EditSource)
            {
                server.LogMessage(LogLevel.Verbose, "CP: changing source of '{0}'.", oldRealm.Name);

                string previewName = realmName + ".preview";

                string tempFile = Path.GetTempFileName();
                try
                {
                    File.WriteAllText(tempFile, text);
                    try
                    {
                        RealmEditingOutcome outcome = server.LoadRealm(
                            previewName, tempFile, factoryName, oldRealm.Owner);

                        if (outcome != RealmEditingOutcome.Success)
                            return outcome;
                    }
                    catch (RealmLoadingException)
                    {
                        return RealmEditingOutcome.VMError;
                    }

                    // successfully changed
                    server.ReplaceRealm(previewName, realmName);
                    if (changingFactory)
                        server.SaveRealms();
                    return RealmEditingOutcome.Success;
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
            else
            {
                // permission denied
                return RealmEditingOutcome.PermissionDenied;
            }
        }

        public string GetRealmIndexPath(string name)
        {
            Realm realm = server.GetRealm(name);

            if (realm == null)
                throw new ArgumentException("No such realm", "name");

            string result = Path.Combine(server.IndexPath, realm.Name);
            if (Directory.Exists(result))
                return Path.GetFullPath(result);
            else
                return null;
        }

        public string GetRealmOwner(string name)
        {
            Realm realm = server.GetRealm(name);

            if (realm == null)
                throw new ArgumentException("No such realm", "name");

            if (realm.Owner == null)
                return "";
            else
                return realm.Owner.Name;
        }

        public RealmAccessLevel GetAccessLevel(string playerName, string realmName)
        {
            if (playerName == null)
                throw new ArgumentNullException("playerName");
            if (realmName == null)
                throw new ArgumentNullException("realmName");

            Player player = server.FindPlayer(playerName);
            if (player == null)
                return RealmAccessLevel.Invalid;

            Realm realm = server.GetRealm(realmName);
            if (realm == null)
                return RealmAccessLevel.Invalid;

            return realm.GetAccessLevel(player);
        }

        public RealmPrivacyLevel GetRealmPrivacy(string name)
        {
            Realm realm = server.GetRealm(name);

            if (realm == null)
                throw new ArgumentException("No such realm", "name");

            return realm.PrivacyLevel;
        }

        public bool SetRealmPrivacy(string playerName, string realmName, RealmPrivacyLevel level)
        {
            Realm realm = server.GetRealm(realmName);
            if (realm == null)
                return false;

            Player player = server.FindPlayer(playerName);
            if (player == null)
                return false;

            if (realm.GetAccessLevel(player) < RealmAccessLevel.EditSettings)
                return false;

            realm.PrivacyLevel = level;
            server.SaveRealms();
            return true;
        }

        public void GetRealmAccessList(string realmName,
            out string[] aclNames, out RealmAccessLevel[] aclLevels)
        {
            Realm realm = server.GetRealm(realmName);

            if (realm == null)
                throw new ArgumentException("No such realm", "realmName");

            RealmAccessListEntry[] entries = realm.AccessList;

            aclNames = new string[entries.Length];
            aclLevels = new RealmAccessLevel[entries.Length];

            for (int i = 0; i < entries.Length; i++)
            {
                aclNames[i] = entries[i].Player.Name;
                aclLevels[i] = entries[i].Level;
            }
        }

        public bool SetRealmAccessList(string playerName, string realmName,
            string[] aclNames, RealmAccessLevel[] aclLevels)
        {
            if (aclNames == null)
                throw new ArgumentNullException("aclNames");
            if (aclLevels == null)
                throw new ArgumentNullException("aclLevels");
            if (aclNames.Length != aclLevels.Length)
                throw new ArgumentException("aclNames and aclLevels must have the same number of elements");

            Realm realm = server.GetRealm(realmName);
            if (realm == null)
                return false;

            Player player = server.FindPlayer(playerName);
            if (player == null)
                return false;

            RealmAccessLevel playerLevel = realm.GetAccessLevel(player);
            if (playerLevel < RealmAccessLevel.EditAccess)
                return false;

            Dictionary<Player,RealmAccessLevel> currentList = new Dictionary<Player,RealmAccessLevel>();
            foreach (RealmAccessListEntry entry in realm.AccessList)
                currentList[entry.Player]=entry.Level;

            RealmAccessListEntry[] entries = new RealmAccessListEntry[aclNames.Length];

            for (int i = 0; i < aclNames.Length; i++)
            {
                Player victim = server.FindPlayer(aclNames[i]);
                if (victim == null)
                    throw new ArgumentException("No such player: " + aclNames[i], "aclNames");
                else if (victim.IsGuest)
                    throw new ArgumentException("Guests may not be added to access lists");

                RealmAccessLevel assignedLevel = aclLevels[i];
                RealmAccessLevel currentLevel = realm.GetAccessLevel(victim);

                if (currentLevel != assignedLevel &&
                    (assignedLevel >= playerLevel || currentLevel >= playerLevel))
                    throw new ArgumentException("You may only edit levels below your own access level");

                entries[i] = new RealmAccessListEntry(victim, assignedLevel);
            }

            realm.AccessList = entries;
            server.SaveRealms();
            return true;
        }

        public bool IsRealmCondemned(string name)
        {
            Realm realm = server.GetRealm(name);

            if (realm == null)
                throw new ArgumentException("No such realm", "name");

            return realm.IsCondemned;
        }

        public string[][] GetWhoList()
        {
            return server.GetWhoList();
        }

        public void Shutdown(string reason)
        {
            server.Shutdown(reason);
        }
    }
}
