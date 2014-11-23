// send non-player game output to the console?
#define CONSOLE_SPAM

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Serialization;
using Textfyre.VM;
using System.ComponentModel;

namespace Guncho
{
    /// <summary>
    /// Describes the level of access that a player has to a realm.
    /// </summary>
    public enum RealmAccessLevel
    {
        /// <summary>
        /// The realm or player doesn't exist.
        /// </summary>
        Invalid,
        /// <summary>
        /// The player isn't allowed to enter the realm.
        /// </summary>
        Banned,
        /// <summary>
        /// The player isn't told of the realm's existence.
        /// </summary>
        Hidden,
        /// <summary>
        /// The player can see that the realm exists.
        /// </summary>
        Visible,
        /// <summary>
        /// The player can teleport into the realm.
        /// </summary>
        Invited,
        /// <summary>
        /// The player can view the realm's source and index.
        /// </summary>
        ViewSource,
        /// <summary>
        /// The player can make changes to the realm source.
        /// </summary>
        EditSource,
        /// <summary>
        /// The player can change the realm's metadata.
        /// </summary>
        EditSettings,
        /// <summary>
        /// The player can change other players' access to the realm.
        /// </summary>
        EditAccess,
        /// <summary>
        /// The player can make changes that are drastic or permanent,
        /// such as deleting the realm.
        /// </summary>
        SafetyOff,

        [Browsable(false)]
        OWNER = SafetyOff,
        [Browsable(false)]
        ADMIN = SafetyOff
    }

    /// <summary>
    /// Describes the default visibility and accessibility of a realm
    /// to players other than the owner.
    /// </summary>
    public enum RealmPrivacyLevel
    {
        /// <summary>
        /// Players aren't allowed to enter the realm.
        /// </summary>
        Private,
        /// <summary>
        /// Players aren't told of the realm's existence.
        /// </summary>
        Hidden,
        /// <summary>
        /// Players can see that the realm exists.
        /// </summary>
        Public,
        /// <summary>
        /// Players can jump to the realm with @teleport.
        /// </summary>
        Joinable,
        /// <summary>
        /// Players can view the realm's source code.
        /// </summary>
        Viewable,
    }

    public struct RealmAccessListEntry
    {
        public readonly Player Player;
        public readonly RealmAccessLevel Level;

        public RealmAccessListEntry(Player player, RealmAccessLevel level)
        {
            this.Player = player;
            this.Level = level;
        }
    }

    public class Realm
    {
        private readonly Server server;
        private readonly RealmFactory factory;
        private readonly string name, sourceFile, storyFile;
        private readonly Player owner;
        private RealmAccessListEntry[] accessList;

        private RealmPrivacyLevel privacy = RealmPrivacyLevel.Public;

        private bool condemned;
        private int failureCount;


        public Realm(Server server, RealmFactory factory, string name, string sourceFile,
            string storyFile, Player owner)
        {
            this.server = server;
            this.factory = factory;
            this.name = name;
            this.sourceFile = sourceFile;
            this.storyFile = storyFile;
            this.owner = owner;
            this.accessList = new RealmAccessListEntry[0];

            LoadStorage();
        }

        public Realm(Realm other, string newName)
            : this(other.server, other.factory, newName, other.sourceFile, other.storyFile, other.owner)
        {
            CopySettingsFrom(other);
        }

        public RealmFactory Factory
        {
            get { return factory; }
        }



        public string Name
        {
            get { return name; }
        }

        public RealmPrivacyLevel PrivacyLevel
        {
            get { return privacy; }
            set { privacy = value; }
        }

        public RealmAccessListEntry[] AccessList
        {
            get { return (RealmAccessListEntry[])accessList.Clone(); }
            set { accessList = (RealmAccessListEntry[])value.Clone(); }
        }

        public Player Owner
        {
            get { return owner; }
        }

        public string SourceFile
        {
            get { return sourceFile; }
        }

        public string StoryFile
        {
            get { return storyFile; }
        }

        public bool IsCondemned
        {
            get { return condemned; }
        }

        /// <summary>
        /// Increments the realm's failure count, and condemns the realm if
        /// the configured threshold is reached.
        /// </summary>
        /// <returns><b>true</b> if the realm has exceeded the allowed number
        /// of failures and been condemned</returns>
        public bool IncrementFailureCount()
        {
            if (++failureCount > Properties.Settings.Default.RealmFailuresAllowed)
            {
                condemned = true;
                return true;
            }

            return false;
        }

        public void CopySettingsFrom(Realm other)
        {
            // copy property values (via the property setters)
            //this.RawMode = other.RawMode;
            this.PrivacyLevel = other.PrivacyLevel;
            this.AccessList = other.accessList;

            // note: we don't copy IsCondemned, because editing a realm un-condemns it
        }

        public RealmAccessLevel GetAccessLevel(Player player)
        {
            if (player != null)
            {
                if (player.IsAdmin)
                    return RealmAccessLevel.ADMIN;

                if (player == this.Owner)
                    return RealmAccessLevel.OWNER;
            }

            for (int i = 0; i < accessList.Length; i++)
                if (accessList[i].Player == player)
                    return accessList[i].Level;

            switch (privacy)
            {
                case RealmPrivacyLevel.Private:
                    return RealmAccessLevel.Banned;

                case RealmPrivacyLevel.Hidden:
                    return RealmAccessLevel.Hidden;

                case RealmPrivacyLevel.Public:
                    return RealmAccessLevel.Visible;

                case RealmPrivacyLevel.Joinable:
                    return RealmAccessLevel.Invited;

                case RealmPrivacyLevel.Viewable:
                    return RealmAccessLevel.ViewSource;

                default:
                    // shouldn't happen
                    throw new Exception("BUG");
            }
        }


        #region Persistent Storage

        private readonly Dictionary<string, string> localStorage = new Dictionary<string, string>();

        private void LoadStorage()
        {
            string filename = Path.Combine(
                Properties.Settings.Default.RealmDataPath,
                this.name + ".storage.xml");

            localStorage.Clear();

            if (File.Exists(filename))
            {
                XML.realmStorage root;

                XmlSerializer ser = new XmlSerializer(typeof(XML.realmStorage));
                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    root = (XML.realmStorage)ser.Deserialize(fs);
                }

                if (root != null)
                {
                    if (root.realm.ToLower() != this.name.ToLower())
                        throw new Exception("Storage file doesn't match this realm");

                    if (root.item != null)
                        foreach (XML.storageItemType item in root.item)
                            localStorage.Add(item.key, item.Value);

                    if (root.player != null)
                        foreach (XML.realmStoragePlayer player in root.player)
                            if (player.item != null)
                                foreach (XML.storageItemType item in player.item)
                                    localStorage.Add(player.name + "\0" + item.key, item.Value);
                }
            }
        }

        private void SaveStorage()
        {
            XML.realmStorage root = new Guncho.XML.realmStorage();
            List<XML.storageItemType> items = new List<XML.storageItemType>();
            Dictionary<string, List<XML.storageItemType>> playerDict = new Dictionary<string, List<XML.storageItemType>>();

            char[] zero = { '\0' };
            foreach (KeyValuePair<string, string> pair in localStorage)
            {
                string[] parts = pair.Key.Split(zero, 2);
                if (parts.Length == 0)
                    continue; // shouldn't happen

                XML.storageItemType item = new XML.storageItemType();
                item.Value = pair.Value;

                if (parts.Length == 1)
                {
                    // realm storage
                    item.key = pair.Key;
                    items.Add(item);
                }
                else
                {
                    // player storage
                    List<XML.storageItemType> playerItems;
                    if (playerDict.TryGetValue(parts[0], out playerItems) == false)
                    {
                        playerItems = new List<XML.storageItemType>();
                        playerDict.Add(parts[0], playerItems);
                    }

                    item.key = parts[1];
                    playerItems.Add(item);
                }
            }

            root.realm = this.name;
            root.item = items.ToArray();

            List<XML.realmStoragePlayer> players = new List<XML.realmStoragePlayer>();
            foreach (KeyValuePair<string, List<XML.storageItemType>> pair in playerDict)
            {
                XML.realmStoragePlayer player = new Guncho.XML.realmStoragePlayer();
                player.name = pair.Key;
                player.item = pair.Value.ToArray();
                players.Add(player);
            }

            root.player = players.ToArray();

            string filename = Path.Combine(
                Properties.Settings.Default.RealmDataPath,
                this.name + ".storage.xml");

            XmlSerializer ser = new XmlSerializer(typeof(XML.realmStorage));
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                ser.Serialize(fs, root);
            }
        }

        public string GetRealmStorage(string key)
        {
            lock (localStorage)
            {
                string value;
                if (localStorage.TryGetValue(key, out value))
                    return value;
                else
                    return "";
            }
        }

        public void SetRealmStorage(string key, string value)
        {
            lock (localStorage)
            {
                if (value == null || value.Length == 0)
                    localStorage.Remove(key);
                else
                    localStorage[key] = value;

                SaveStorage();
            }
        }

        public string GetPlayerStorage(Player player, string key)
        {
            return GetRealmStorage(player.Name + "\0" + key);
        }

        public void SetPlayerStorage(Player player, string key, string value)
        {
            SetRealmStorage(player.Name + "\0" + key, value);
        }

        #endregion

    }
}
