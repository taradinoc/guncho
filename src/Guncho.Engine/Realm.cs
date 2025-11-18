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
using System.Diagnostics.Contracts;
using Nito.AsyncEx;
using Guncho.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Guncho.Services;

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
        private readonly IServerConfiguration config = null!;
        private readonly IServiceProvider? services;
        private readonly string sourceFile = null!, storyFile = null!;
        private RealmAccessListEntry[] accessList = null!;

        private RealmPrivacyLevel privacy = RealmPrivacyLevel.Public;

        private bool condemned;
        private int failureCount;

        /// <summary>
        /// Database ID of the realm. Used to load assets from the database.
        /// </summary>
        public int DatabaseId { get; set; }

        /// <summary>
        /// Name of the main file asset to compile (e.g., "story.ni", "main.inf").
        /// If null, uses factory's default.
        /// </summary>
        public string? MainFile { get; set; }


        public Realm(RealmFactory factory, IServerConfiguration config, string name, string sourceFile, string storyFile,
            Player owner, IServiceProvider? services = null)
        {
            this.Factory = factory;
            this.config = config;
            this.Name = name;
            this.sourceFile = sourceFile;
            this.storyFile = storyFile;
            this.Owner = owner;
            this.accessList = [];
            this.services = services;
            // Storage is DB-backed; do not touch XML here
        }

        public Realm(Realm other, string newName)
            : this(other.Factory, other.config, newName, other.sourceFile, other.storyFile, other.Owner, other.services)
        {
            CopySettingsFrom(other);
        }

        public AsyncReaderWriterLock Lock { get; } = new AsyncReaderWriterLock();

        public RealmFactory Factory { get; set; } = null!;

        public string Name { get; set; } = null!;

        public RealmPrivacyLevel PrivacyLevel { get; set; }

        public RealmAccessListEntry[] AccessList
        {
            get { return (RealmAccessListEntry[])accessList.Clone(); }
            set { accessList = (RealmAccessListEntry[])value.Clone(); }
        }

        public Player Owner { get; set; } = null!;

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
            if (++failureCount > config.RealmFailuresAllowed)
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
            // No XML reads here; DB-backed access happens on demand in getters
            localStorage.Clear();
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
                    List<XML.storageItemType>? playerItems;
                    if (playerDict.TryGetValue(parts[0], out playerItems) == false)
                    {
                        playerItems = new List<XML.storageItemType>();
                        playerDict.Add(parts[0], playerItems);
                    }

                    item.key = parts[1];
                    playerItems.Add(item);
                }
            }

            root.realm = this.Name;
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

            // Do not write XML; persistence is handled via StorageRepository in setters
        }

        public string GetRealmStorage(string key)
        {
            // Prefer DB if available
            try
            {
                if (services != null && DatabaseId > 0)
                {
                    using var scope = services.CreateScope();
                    var repo = scope.ServiceProvider.GetService<StorageRepository>();
                    if (repo != null)
                    {
                        var val = repo.GetRealmValueAsync(DatabaseId, key).GetAwaiter().GetResult();
                        if (!string.IsNullOrEmpty(val))
                        {
                            lock (localStorage)
                            {
                                localStorage[key] = val!;
                            }
                            return val!;
                        }
                        return "";
                    }
                }
            }
            catch { }

            lock (localStorage)
            {
                return localStorage.TryGetValue(key, out var value) ? value : "";
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
            }

            // Persist to DB if available
            try
            {
                if (services != null && DatabaseId > 0)
                {
                    using var scope = services.CreateScope();
                    var repo = scope.ServiceProvider.GetService<StorageRepository>();
                    if (repo != null)
                    {
                        // Fire-and-forget to avoid blocking VM thread
                        _ = repo.SetRealmValueAsync(DatabaseId, key, value ?? "");
                    }
                }
            }
            catch { }
        }

        public string GetPlayerStorage(Player player, string key)
        {
            // Prefer DB if available
            try
            {
                if (services != null && DatabaseId > 0)
                {
                    using var scope = services.CreateScope();
                    var repo = scope.ServiceProvider.GetService<StorageRepository>();
                    if (repo != null)
                    {
                        var val = repo.GetPlayerValueAsync(DatabaseId, player.ID, key).GetAwaiter().GetResult();
                        if (!string.IsNullOrEmpty(val))
                        {
                            lock (localStorage)
                            {
                                localStorage[player.Name + "\0" + key] = val!;
                            }
                            return val!;
                        }
                        return "";
                    }
                }
            }
            catch { }

            return GetRealmStorage(player.Name + "\0" + key);
        }

        public void SetPlayerStorage(Player player, string key, string value)
        {
            // Update in-memory cache
            SetRealmStorage(player.Name + "\0" + key, value);

            // Persist to DB if available
            try
            {
                if (services != null && DatabaseId > 0)
                {
                    using var scope = services.CreateScope();
                    var repo = scope.ServiceProvider.GetService<StorageRepository>();
                    if (repo != null)
                    {
                        _ = repo.SetPlayerValueAsync(DatabaseId, player.ID, key, value ?? "");
                    }
                }
            }
            catch { }
        }

        #endregion

    }
}
