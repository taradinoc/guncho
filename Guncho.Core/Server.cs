/* Define COVERUP to clone the starting realm for each new player, so each player's
 * experience is independent of the other players. This does the following:
 * 
 *      - Disables all I/O filtering (tags and commands)
 *      - Loads only from the cache instead of compiling realms on the fly (since
 *        coverup realms must be compiled with an unhacked library)
 *      - Skips the login sequence by logging everyone in as a new Guest automatically
 *      - Causes players to be disconnected when the realm ends, and deleted when
 *        they disconnect
 * 
 * COVERUP mode disguises the MUD as a single-player IF server.
 */
//#define COVERUP

// TODO: use reader/writer locks instead of lock() where appropriate

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using System.Web;

namespace Guncho
{
    delegate void VoidDelegate();

    public class RealmLoadingException : Exception
    {
        public RealmLoadingException(string realmName, Exception innerException)
            : base("The realm '" + realmName + "' could not be loaded", innerException)
        {
        }
    }

    public enum RealmEditingOutcome
    {
        /// <summary>
        /// The realm was successful recompiled and reloaded.
        /// </summary>
        Success,
        /// <summary>
        /// There was no source code to compile.
        /// </summary>
        Missing,
        /// <summary>
        /// Whoever's trying to edit the realm doesn't have the right access level.
        /// </summary>
        PermissionDenied,
        /// <summary>
        /// Inform 7 failed to translate the realm.
        /// </summary>
        NiError,
        /// <summary>
        /// Inform 7 translated the realm, but Inform 6 failed to compile it.
        /// </summary>
        InfError,
        /// <summary>
        /// The realm was compiled, but it couldn't be loaded.
        /// </summary>
        VMError,
    }

    partial class Server
    {
        private readonly int port;
        private readonly ILogger logger;

        private volatile bool running;

        private readonly List<Connection> connections = new List<Connection>();
        private readonly Dictionary<string, NetworkPlayer> players = new Dictionary<string, NetworkPlayer>();
        private readonly Dictionary<string, Realm> realms = new Dictionary<string, Realm>();
        private readonly Dictionary<string, Instance> instances = new Dictionary<string, Instance>();
        private readonly Dictionary<string, RealmFactory> factories = new Dictionary<string, RealmFactory>();
        private readonly Queue<VoidDelegate> eventQueue = new Queue<VoidDelegate>();
        private readonly Thread eventThread;
        private readonly PriorityQueue<TimedEvent> timedEvents = new PriorityQueue<TimedEvent>();
        private readonly Dictionary<Instance, TimedEvent> timedEventsByInstance = new Dictionary<Instance, TimedEvent>();
        private readonly AutoResetEvent mainLoopEvent = new AutoResetEvent(false);

        public Server(int port, ILogger logger)
        {
            if (logger == null)
                throw new ArgumentNullException("logger");

            this.port = port;
            this.logger = logger;

            try
            {
                eventThread = new Thread(EventThreadProc);
                eventThread.Name = "Server Events";

                LoadPlayers();
                LoadRealms();

                if (GetRealm(Properties.Settings.Default.StartRealmName) == null)
                    throw new InvalidOperationException(
                        string.Format("Couldn't load initial realm '{0}'",
                                        Properties.Settings.Default.StartRealmName));

                ControllerFactory.Register(this);
            }
            catch (Exception ex)
            {
                LogException(ex);
                throw;
            }
        }

        public string IndexPath
        {
            get { return Path.Combine(Properties.Settings.Default.CachePath, "Index"); }
        }

        public void LogMessage(LogLevel level, string text)
        {
            logger.LogMessage(level, text);
        }

        public void LogMessage(LogLevel level, string format, params object[] args)
        {
            logger.LogMessage(level, string.Format(format, args));
        }

        public void LogException(Exception ex)
        {
            logger.LogMessage(LogLevel.Error, ex.ToString());
        }

        private void EventThreadProc()
        {
            try
            {
                int granularity = Properties.Settings.Default.EventGranularity;

                while (running)
                {
                    VoidDelegate eventToRun;

                    lock (eventQueue)
                    {
                        if (eventQueue.Count == 0)
                            Monitor.Wait(eventQueue, granularity);
                        if (eventQueue.Count == 0)
                            eventToRun = null;
                        else
                            eventToRun = eventQueue.Dequeue();
                    }

                    if (eventToRun != null)
                    {
                        eventToRun();
                    }
                    else
                    {
                        CheckTimedEvents();
#if !DEBUG
                        CheckWatchdogs();
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
                throw;
            }
        }

        private class TimedEvent
        {
            public readonly Instance Instance;
            public readonly int Interval;
            public DateTime Time;
            public bool Deleted;

            public TimedEvent(Instance instance, int interval)
            {
                this.Instance = instance;
                this.Time = DateTime.Now.AddSeconds(interval);
                this.Interval = interval;
            }
        }

        private void CheckTimedEvents()
        {
            DateTime now = DateTime.Now;

            lock (timedEvents)
            {
                while (timedEvents.Count > 0)
                {
                    TimedEvent ev = timedEvents.Peek();

                    if (ev.Deleted)
                    {
                        timedEvents.Dequeue();
                        continue;
                    }

                    if (ev.Time > now)
                        break;

                    timedEvents.Dequeue();
                    ev.Instance.QueueInput("$rtevent");

                    ev.Time = now.AddSeconds(ev.Interval);
                    timedEvents.Enqueue(ev, ev.Time.ToFileTime());
                }
            }
        }

        private void CheckWatchdogs()
        {
            DateTime now = DateTime.Now;

            lock (realms)
            {
                foreach (Instance r in instances.Values)
                {
                    if (now >= r.WatchdogTime)
                    {
                        Instance failed = r;
                        QueueEvent(delegate { HandleInstanceFailure(failed, "frozen"); });
                    }
                }
            }
        }

        public void SetEventInterval(Instance instance, int seconds)
        {
            if (instance == null)
                throw new ArgumentNullException("realm");
            if (seconds < 0)
                throw new ArgumentOutOfRangeException("seconds");

            lock (timedEvents)
            {
                TimedEvent prev;
                if (timedEventsByInstance.TryGetValue(instance, out prev))
                {
                    if (prev.Interval != seconds)
                        prev.Deleted = true;
                    else
                        return;
                }

                if (seconds == 0)
                {
                    timedEventsByInstance.Remove(instance);
                }
                else
                {
                    TimedEvent ev = new TimedEvent(instance, seconds);
                    timedEventsByInstance[instance] = ev;
                    timedEvents.Enqueue(ev, ev.Time.ToFileTime());
                }
            }
        }

        private void LoadRealms()
        {
            // load factories first
            string defaultFactory = null;

            factories.Clear();
            foreach (string subPath in Directory.GetDirectories(Properties.Settings.Default.NiInstallationsPath))
            {
                string nibin, i6bin;
                if (InformRealmFactory.FindCompilers(Path.Combine(subPath, "Compilers"), out nibin, out i6bin))
                {
                    string version = Path.GetFileName(subPath);

                    string niCompilerPath = nibin;
                    string niExtensionDir = Path.Combine(subPath, "Inform7" + Path.DirectorySeparatorChar + "Extensions");
                    string infCompilerPath = i6bin;
                    string infLibraryDir = Path.Combine(subPath, "Library" + Path.DirectorySeparatorChar + "Natural");

                    // game factory
                    RealmFactory factory = new InformGameRealmFactory(this, version,
                        niCompilerPath, niExtensionDir, infCompilerPath, infLibraryDir);

                    if (defaultFactory == null || version.CompareTo(defaultFactory) < 0)
                        defaultFactory = version;

                    factories.Add(version, factory);

                    // bot factory
                    niExtensionDir = Path.Combine(subPath, @"Inform7\BotExtensions");
                    if (Directory.Exists(niExtensionDir))
                    {
                        string name = "bot_" + version;
                        factory = new InformBotRealmFactory(this, name,
                            niCompilerPath, niExtensionDir, infCompilerPath, infLibraryDir);
                        factories.Add(name, factory);
                    }
                }
            }

            // load realm index
            XML.realmIndex index;
            string dataPath = Properties.Settings.Default.RealmDataPath;

            XmlSerializer ser = new XmlSerializer(typeof(XML.realmIndex));
            using (FileStream fs = new FileStream(
                Path.Combine(dataPath, Properties.Settings.Default.RealmsFileName),
                FileMode.Open, FileAccess.Read))
            {
                index = (XML.realmIndex)ser.Deserialize(fs);
            }

            // load each realm
            lock (realms)
            {
                realms.Clear();
                foreach (XML.realmIndexRealm entry in index.realms)
                {
                    Player owner = FindPlayer(entry.owner);
                    try
                    {
                        LoadRealm(entry.name, Path.Combine(dataPath, entry.src),
                            entry.factory ?? defaultFactory, owner);
                    }
                    catch (RealmLoadingException rle)
                    {
                        LogMessage(LogLevel.Error,
                            "Skipping realm '{0}': {1} while loading: {2}",
                            entry.name,
                            rle.InnerException.GetType().Name,
                            rle.InnerException.Message);
                    }

                    Realm realm = GetRealm(entry.name);
                    if (realm != null)
                    {
                        switch (entry.privacy)
                        {
                            case Guncho.XML.privacyType.hidden:
                                realm.PrivacyLevel = RealmPrivacyLevel.Hidden;
                                break;

                            case Guncho.XML.privacyType.@private:
                                realm.PrivacyLevel = RealmPrivacyLevel.Private;
                                break;

                            case Guncho.XML.privacyType.@public:
                                realm.PrivacyLevel = RealmPrivacyLevel.Public;
                                break;

                            case Guncho.XML.privacyType.joinable:
                                realm.PrivacyLevel = RealmPrivacyLevel.Joinable;
                                break;

                            case Guncho.XML.privacyType.viewable:
                                realm.PrivacyLevel = RealmPrivacyLevel.Viewable;
                                break;
                        }

                        if (entry.access != null && entry.access.Length > 0)
                        {
                            List<RealmAccessListEntry> entries = new List<RealmAccessListEntry>();
                            foreach (XML.realmIndexRealmAccess xent in entry.access)
                            {
                                Player p = FindPlayer(xent.player);
                                if (p == null)
                                {
                                    LogMessage(LogLevel.Warning,
                                        "Nonexistent player in '{0}' ACL: '{1}'",
                                        entry.name, xent.player);
                                    continue;
                                }
                                RealmAccessLevel level;
                                switch (xent.level)
                                {
                                    case Guncho.XML.levelType.banned:
                                        level = RealmAccessLevel.Banned;
                                        break;

                                    case Guncho.XML.levelType.editAccess:
                                        level = RealmAccessLevel.EditAccess;
                                        break;

                                    case Guncho.XML.levelType.editSettings:
                                        level = RealmAccessLevel.EditSettings;
                                        break;

                                    case Guncho.XML.levelType.editSource:
                                        level = RealmAccessLevel.EditSource;
                                        break;

                                    case Guncho.XML.levelType.hidden:
                                        level = RealmAccessLevel.Hidden;
                                        break;

                                    case Guncho.XML.levelType.invited:
                                        level = RealmAccessLevel.Invited;
                                        break;

                                    case Guncho.XML.levelType.safetyOff:
                                        level = RealmAccessLevel.SafetyOff;
                                        break;

                                    case Guncho.XML.levelType.viewSource:
                                        level = RealmAccessLevel.ViewSource;
                                        break;

                                    case Guncho.XML.levelType.visible:
                                        level = RealmAccessLevel.Visible;
                                        break;

                                    default:
                                        LogMessage(LogLevel.Warning,
                                            "Nonexistent level in '{0}' ACL: '{1}'",
                                            entry.name, xent.level);
                                        continue;
                                }
                                entries.Add(new RealmAccessListEntry(p, level));
                            }
                            realm.AccessList = entries.ToArray();
                        }
                    }
                }
            }
        }

        public void SaveRealms()
        {
            XML.realmIndex index = new Guncho.XML.realmIndex();
            List<XML.realmIndexRealm> entries = new List<Guncho.XML.realmIndexRealm>();

            lock (realms)
            {
                foreach (Realm r in realms.Values)
                {
                    XML.realmIndexRealm item = new Guncho.XML.realmIndexRealm();
                    item.name = r.Name;
                    item.src = Path.GetFileName(r.SourceFile);
                    item.owner = r.Owner.Name;
                    item.factory = r.Factory.Name;
                    switch (r.PrivacyLevel)
                    {
                        case RealmPrivacyLevel.Hidden:
                            item.privacy = Guncho.XML.privacyType.hidden;
                            break;

                        case RealmPrivacyLevel.Private:
                            item.privacy = Guncho.XML.privacyType.@private;
                            break;

                        case RealmPrivacyLevel.Public:
                            item.privacy = Guncho.XML.privacyType.@public;
                            break;

                        case RealmPrivacyLevel.Joinable:
                            item.privacy = Guncho.XML.privacyType.joinable;
                            break;

                        case RealmPrivacyLevel.Viewable:
                            item.privacy = Guncho.XML.privacyType.viewable;
                            break;
                    }

                    List<XML.realmIndexRealmAccess> acl = new List<XML.realmIndexRealmAccess>();
                    foreach (RealmAccessListEntry entry in r.AccessList)
                    {
                        XML.realmIndexRealmAccess xent = new XML.realmIndexRealmAccess();
                        xent.player = entry.Player.Name;
                        switch (entry.Level)
                        {
                            case RealmAccessLevel.Banned:
                                xent.level = Guncho.XML.levelType.banned;
                                break;

                            case RealmAccessLevel.EditAccess:
                                xent.level = Guncho.XML.levelType.editAccess;
                                break;

                            case RealmAccessLevel.EditSettings:
                                xent.level = Guncho.XML.levelType.editSettings;
                                break;

                            case RealmAccessLevel.EditSource:
                                xent.level = Guncho.XML.levelType.editSource;
                                break;

                            case RealmAccessLevel.Hidden:
                                xent.level = Guncho.XML.levelType.hidden;
                                break;

                            case RealmAccessLevel.Invited:
                                xent.level = Guncho.XML.levelType.invited;
                                break;

                            case RealmAccessLevel.SafetyOff:
                                xent.level = Guncho.XML.levelType.safetyOff;
                                break;

                            case RealmAccessLevel.ViewSource:
                                xent.level = Guncho.XML.levelType.viewSource;
                                break;

                            case RealmAccessLevel.Visible:
                                xent.level = Guncho.XML.levelType.visible;
                                break;
                        }
                        acl.Add(xent);
                    }
                    item.access = acl.ToArray();

                    entries.Add(item);
                }
            }

            index.realms = entries.ToArray();

            using (ReplacingStream stream = new ReplacingStream(
                Path.Combine(Properties.Settings.Default.RealmDataPath,
                             Properties.Settings.Default.RealmsFileName)))
            {
                XmlSerializer ser = new XmlSerializer(typeof(XML.realmIndex));
                ser.Serialize(stream, index);
            }
        }

        /// <summary>
        /// Loads a realm from the cache, or compiles it if necessary, and adds it to the realm list.
        /// </summary>
        /// <param name="realmName">The name of the realm to load. A realm by this name must not already exist.</param>
        /// <param name="sourceFile">The path to the realm source file.</param>
        /// <param name="factoryName">The name of the realm factory to use.</param>
        /// <param name="owner">The player who will own the realm.</param>
        /// <returns>A <see cref="RealmEditingOutcome"/> indicating whether the realm was compiled
        /// successfully. This method will not return <see cref="RealmEditingOutcome.VMError"/>; if
        /// a VM error occurs, it will throw <see cref="RealmLoadingException"/>.</returns>
        /// <exception cref="ArgumentException">
        /// A realm with the specified name already exists.
        /// </exception>
        /// <exception cref="RealmLoadingException">
        /// The realm was compiled, but an exception occurred while loading it.
        /// </exception>
        public RealmEditingOutcome LoadRealm(string realmName, string sourceFile, string factoryName,
            Player owner)
        {
            if (realmName == null)
                throw new ArgumentNullException("realmName");
            if (sourceFile == null)
                throw new ArgumentNullException("sourceFile");
            if (owner == null)
                throw new ArgumentNullException("owner");
            if (GetRealm(realmName) != null)
                throw new ArgumentException("A realm with this name is already loaded", "realmName");

            RealmFactory factory;
            if (factories.TryGetValue(factoryName, out factory) == false)
                throw new ArgumentException("Unrecognized realm factory", "factoryName");

            lock (realms)
            {
#if COVERUP
                string cachedFile = Path.Combine(Properties.Settings.Default.CachePath, realmName + ".ulx");
                if (!File.Exists(cachedFile))
                    return;
#else
                if (!File.Exists(sourceFile))
                {
                    LogMessage(LogLevel.Error,
                        "Skipping realm '{0}': source file '{1}' missing",
                        realmName, sourceFile);
                    return RealmEditingOutcome.Missing;
                }

                // check for a cached copy that's no older than the source
                string cachePath = Properties.Settings.Default.CachePath;
                string cachedFile = Path.Combine(cachePath, realmName + ".ulx");
                bool needCompile = true;
                RealmEditingOutcome outcome = RealmEditingOutcome.Success;

                if (File.Exists(cachedFile))
                {
                    DateTime sourceTime = File.GetLastWriteTime(sourceFile);
                    DateTime cacheTime = File.GetLastWriteTime(cachedFile);
                    if (cacheTime >= sourceTime)
                        needCompile = false;
                }

            compile_realm:

                if (needCompile)
                    outcome = factory.CompileRealm(realmName, sourceFile, cachedFile);

                if (outcome != RealmEditingOutcome.Success)
                {
                    LogMessage(LogLevel.Verbose,
                        "Removing realm '{0}' (failed to compile in LoadRealm).", realmName);
                    realms.Remove(realmName);
                    return outcome;
                }
#endif

                Realm realm;
                try
                {
                    realm = factory.LoadRealm(realmName, sourceFile, cachedFile, owner);
#if COVERUP
                    r.RawMode = true;
#endif
                    realms.Add(realmName.ToLower(), realm);
                }
                catch (Exception ex)
                {
                    if (!needCompile)
                    {
                        // the file from the cache is no good, so let's try compiling it ourselves
                        needCompile = true;
                        goto compile_realm;
                    }
                    else
                    {
                        throw new RealmLoadingException(realmName, ex);
                    }
                }

                if (realm.AutoActivate)
                {
                    Instance inst = factory.LoadInstance(realm, realm.Name);
                    lock (instances)
                    {
                        if (!instances.ContainsKey(inst.Name.ToLower()))
                        {
                            instances.Add(inst.Name.ToLower(), inst);
                            inst.Activate();
                        }
                    }
                }
            }

            return RealmEditingOutcome.Success;
        }

        private Instance LoadInstance(Realm realm, string name)
        {
            Instance result;

            lock (instances)
            {
                if (GetInstance(name) != null)
                    throw new ArgumentException("An instance with this name is already loaded", "name");
                result = realm.Factory.LoadInstance(realm, name);
                instances.Add(name.ToLower(), result);
            }

            return result;
        }

        public Realm GetRealm(string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            Realm result;
            lock (realms)
                realms.TryGetValue(name.ToLower(), out result);
            return result;
        }

        public Instance GetInstance(string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            Instance result;
            lock (instances)
                instances.TryGetValue(name.ToLower(), out result);
            return result;
        }

        private GameInstance GetDefaultInstance(Realm realm)
        {
            lock (instances)
            {
                Instance inst = GetInstance(realm.Name);

                if (inst == null)
                    inst = LoadInstance(realm, realm.Name);

                return inst as GameInstance;
            }
        }

        private Instance[] GetAllInstances(Realm realm)
        {
            List<Instance> result = new List<Instance>();

            lock (instances)
            {
                foreach (Instance inst in instances.Values)
                    if (inst.Realm == realm)
                        result.Add(inst);
            }

            return result.ToArray();
        }

        public string[] ListRealms()
        {
            lock (realms)
            {
                List<string> result = new List<string>(realms.Count);
                foreach (Realm r in realms.Values)
                    result.Add(r.Name);
                return result.ToArray();
            }
        }

        public string[] ListRealmFactories()
        {
            List<string> result = new List<string>(factories.Count);
            foreach (string key in factories.Keys)
                result.Add(key);
            return result.ToArray();
        }

        /// <summary>
        /// Renames a realm, moving the source files and players to the new name
        /// (and resetting the realm).
        /// </summary>
        /// <param name="fromName">The current name of the realm which is to be
        /// renamed.</param>
        /// <param name="toName">The destination name of the realm. The existing
        /// realm with this name will be replaced.</param>
        /// <remarks>
        /// A realm must already exist with the target name; it will be replaced
        /// and its players will be transferred to the replacement realm. The new
        /// realm will have the same owner and other settings (e.g. privacy) as
        /// the old realm.
        /// </remarks>
        public void ReplaceRealm(string fromName, string toName)
        {
            if (fromName == null)
                throw new ArgumentNullException("fromName");
            if (toName == null)
                throw new ArgumentNullException("toName");

            LogMessage(LogLevel.Spam, "Renaming realm '{0}' to '{1}'.", fromName, toName);

            if (fromName.ToLower() == toName.ToLower())
                return;

            lock (realms)
            {
                lock (instances)
                {
                    Realm replacement = GetRealm(fromName);
                    if (replacement == null)
                        throw new ArgumentException("No such realm", "fromName");

                    Realm original = GetRealm(toName);
                    if (original == null)
                        throw new ArgumentException("No such realm", "toName");

                    Instance[] origInstances = GetAllInstances(original);
                    Instance[] replcInstances = GetAllInstances(replacement);
                    var saved = new Dictionary<string, Dictionary<Player, string>>();

                    // extract players from running original instances
                    foreach (Instance inst in origInstances)
                    {
                        GameInstance gi = inst as GameInstance;
                        if (gi != null)
                        {
                            var dict = new Dictionary<Player, string>();
                            saved.Add(gi.Name, dict);
                            gi.ExportPlayerPositions(dict);
                        }
                        SetEventInterval(inst, 0);
                        inst.PolitelyDispose();
                        instances.Remove(inst.Name.ToLower());
                    }

                    // there shouldn't be any players in replacement instances, but
                    // if there are for some reason, dump them in the new default instance
                    foreach (Instance inst in replcInstances)
                    {
                        GameInstance gi = inst as GameInstance;
                        if (gi != null)
                        {
                            Dictionary<Player, string> dict;
                            if (saved.TryGetValue(toName, out dict) == false)
                            {
                                dict = new Dictionary<Player, string>();
                                saved.Add(toName, dict);
                            }
                            gi.ExportPlayerPositions(dict);
                        }
                        SetEventInterval(inst, 0);
                        inst.Dispose();
                        instances.Remove(inst.Name.ToLower());
                    }

                    LogMessage(LogLevel.Spam, "Removing realms '{0}' and '{1}'.", fromName, toName);
                    realms.Remove(fromName.ToLower());
                    realms.Remove(toName.ToLower());

                    // move source file
                    string fromSource = replacement.SourceFile;
                    string toSource = original.SourceFile;
                    if (File.Exists(fromSource))
                    {
                        // TODO: archive the old file so it can be reverted later instead of deleting it
                        File.Delete(toSource);
                        File.Move(fromSource, toSource);
                    }

                    // delete/move cached realm and index
                    string cachePath = Properties.Settings.Default.CachePath;
                    string toCached = Path.Combine(cachePath, toName + ".ulx");
                    File.Delete(toCached);
                    string fromCached = Path.Combine(cachePath, fromName + ".ulx");
                    if (File.Exists(fromCached))
                        File.Move(fromCached, toCached);

                    string fromIndex = Path.Combine(IndexPath, fromName);
                    string toIndex = Path.Combine(IndexPath, toName);
                    if (Directory.Exists(fromIndex))
                        RealmFactory.CopyDirectory(fromIndex, toIndex);

                    // reload realm
                    LoadRealm(toName, toSource, replacement.Factory.Name, replacement.Owner);
                    Realm newRealm = GetRealm(toName);
                    if (newRealm != null)
                    {
                        newRealm.CopySettingsFrom(original);

                        LogMessage(LogLevel.Verbose, "Reloaded '{0}'.", newRealm.Name);

                        foreach (var instPair in saved)
                        {
                            GameInstance inst = (GameInstance)LoadInstance(newRealm, instPair.Key);
                            foreach (KeyValuePair<Player, string> pair in instPair.Value)
                            {
                                pair.Key.NotifyInstanceReloading();
                                EnterInstance(pair.Key, inst, pair.Value);
                            }
                        }
                    }
                    else
                    {
                        LogMessage(LogLevel.Error, "Failed to reload '{0}' in ReplaceRealm.", toName);

                        GameInstance startInst = GetDefaultInstance(GetRealm(Properties.Settings.Default.StartRealmName));
                        foreach (Dictionary<Player, string> positions in saved.Values)
                            foreach (KeyValuePair<Player, string> pair in positions)
                            {
                                pair.Key.WriteLine("[The realm has failed.]");
                                EnterInstance(pair.Key, startInst, pair.Value);
                            }
                    }
                }
            }
        }

        public static bool IsValidPlayerName(string name)
        {
            if (name.Length < 1 || name.Length > 16)
                return false;

            if (!char.IsLetter(name[0]))
                return false;

            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    return false;
            }

            if (name.StartsWith("guest", StringComparison.CurrentCultureIgnoreCase))
                return false;

            return true;
        }

        public Player CreatePlayer(string newName, string pwdSalt, string pwdHash)
        {
#if COVERUP
            // no player creation in coverup mode
            return null;
#else
            if (newName == null)
                throw new ArgumentNullException("newName");
            if (pwdSalt == null)
                throw new ArgumentNullException("pwdSalt");
            if (pwdHash == null)
                throw new ArgumentNullException("pwdHash");

            if (!IsValidPlayerName(newName))
                return null;

            lock (players)
            {
                string key = newName.ToLower();
                if (players.ContainsKey(key))
                    return null;

                // find an unused ID
                Dictionary<int,Player> playersById = new Dictionary<int,Player>(players.Count);
                foreach (NetworkPlayer p in players.Values)
                    playersById.Add(p.ID, p);

                int id = 1;
                while (playersById.ContainsKey(id))
                    id++;

                NetworkPlayer result = new NetworkPlayer(id, newName, false);
                result.PasswordSalt = pwdSalt;
                result.PasswordHash = pwdHash;

                players.Add(newName.ToLower(), result);
                SavePlayers();
                return result;
            }
#endif
        }

        public static bool IsValidRealmName(string name)
        {
            if (name != name.Trim())
                return false;

            if (name.Length == 0)
                return false;

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;

            return true;
        }

        public Realm CreateRealm(Player newOwner, string newName, string newFactoryName)
        {
#if COVERUP
            // no realm creation in coverup mode
            return null;
#else
            string key = newName.ToLower();

            lock (realms)
            {
                if (!IsValidRealmName(newName))
                    return null;

                if (realms.ContainsKey(key))
                    return null;

                RealmFactory factory;
                if (factories.TryGetValue(newFactoryName, out factory) == false)
                    return null;

                // enforce limit on number of realms per player
                if (!newOwner.IsAdmin)
                {
                    int count = 0;
                    foreach (Realm r in realms.Values)
                        if (r.Owner == newOwner)
                            count++;

                    if (count >= Properties.Settings.Default.MaxRealmsPerPlayer)
                        return null;
                }

                string source = NewSourceFileName(newOwner.Name, newName, factory.SourceFileExtension);
                File.WriteAllText(source, factory.GetInitialSourceText(newOwner.Name, newName));
                try
                {
                    LoadRealm(newName, source, newFactoryName, newOwner);
                }
                catch
                {
                    return null;
                }
                SaveRealms();
                return GetRealm(newName);
            }
#endif
        }

        public bool DeleteRealm(Realm realm)
        {
#if COVERUP
            // no realm deletion in coverup mode
            return false;
#else
            Realm startRealm;

            lock (realms)
            {
                startRealm = GetRealm(Properties.Settings.Default.StartRealmName);

                if (realm == startRealm)
                    return false;

                LogMessage(LogLevel.Notice, "Deleting realm '{0}'", realm.Name);
                realms.Remove(realm.Name.ToLower());
            }

            // close all instances
            GameInstance startInstance = GetDefaultInstance(startRealm);
            foreach (GameInstance inst in GetAllInstances(realm))
            {
                if (inst.IsActive)
                {
                    Player[] players = inst.ListPlayers();
                    foreach (Player p in players)
                        EnterInstance(p, startInstance);
                }

                SetEventInterval(inst, 0);
                inst.PolitelyDispose();
            }

            // delete the z-code and index, but leave the source just in case
            try
            {
                string zfile = Path.Combine(
                    Properties.Settings.Default.CachePath,
                    realm.Name + ".ulx");
                File.Delete(zfile);
            }
            catch (IOException) { }

            try
            {
                string index = Path.Combine(IndexPath, realm.Name);
                Directory.Delete(index, true);
            }
            catch (IOException) { }

            SaveRealms();
            return true;
#endif
        }

        private static string NewSourceFileName(string ownerName, string realmName, string ext)
        {
            if (ext.Length > 0 && !ext.StartsWith("."))
                ext = "." + ext;

            StringBuilder sb = new StringBuilder();
            sb.Append(ownerName.ToLower());
            sb.Append('_');
            sb.Append(realmName.ToLower());

            // replace invalid filename characters with underscores
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < sb.Length; i++)
                if (Array.IndexOf(invalid, sb[i]) >= 0)
                    sb[i] = '_';

            // collapse multiple underscores
            for (int i = sb.Length - 1; i > 0; i--)
                if (sb[i] == '_' && sb[i - 1] == '_')
                    sb.Remove(i, 1);

            string fn = sb.ToString();
            string dir = Properties.Settings.Default.RealmDataPath;
            string path = Path.Combine(dir, fn + ".ni");

            if (File.Exists(path))
            {
                // add a numeric suffix
                int i = 1;
                do
                {
                    path = Path.Combine(dir, fn + "[" + i.ToString() + "]" + ext);
                    i++;
                } while (File.Exists(path));
            }

            return path;
        }

        private void LoadPlayers()
        {
            XML.playerIndex index;

            XmlSerializer ser = new XmlSerializer(typeof(XML.playerIndex));
            using (FileStream fs = new FileStream(
                Path.Combine(Properties.Settings.Default.RealmDataPath,
                             Properties.Settings.Default.PlayersFileName),
                FileMode.Open, FileAccess.Read))
            {
                index = (XML.playerIndex)ser.Deserialize(fs);
            }

            players.Clear();
            foreach (XML.playerIndexPlayersPlayer entry in index.Item.player)
            {
                NetworkPlayer player = new NetworkPlayer(entry.id, entry.name,
                    entry.adminSpecified && entry.admin);
                player.PasswordSalt = entry.pwdSalt;
                player.PasswordHash = entry.pwdHash;
                players.Add(player.Name.ToLower(), player);

                if (entry.attribute != null)
                    foreach (XML.playerIndexPlayersPlayerAttribute attr in entry.attribute)
                        player.SetAttribute(attr.name, attr.Value);
            }
        }

        public void SavePlayers()
        {
            XML.playerIndex index = new Guncho.XML.playerIndex();
            index.Item = new Guncho.XML.playerIndexPlayers();
            List<XML.playerIndexPlayersPlayer> entries = new List<Guncho.XML.playerIndexPlayersPlayer>();

            lock (players)
            {
                foreach (NetworkPlayer p in players.Values)
                {
                    if (p.IsGuest)
                        continue;

                    XML.playerIndexPlayersPlayer item = new Guncho.XML.playerIndexPlayersPlayer();
                    if (p.IsAdmin)
                    {
                        item.admin = true;
                        item.adminSpecified = true;
                    }
                    item.id = p.ID;
                    item.name = p.Name;
                    item.pwdHash = p.PasswordHash;
                    item.pwdSalt = p.PasswordSalt;

                    List<XML.playerIndexPlayersPlayerAttribute> attrs = new List<Guncho.XML.playerIndexPlayersPlayerAttribute>();
                    foreach (KeyValuePair<string, string> pair in p.GetAllAttributes())
                    {
                        XML.playerIndexPlayersPlayerAttribute attr = new Guncho.XML.playerIndexPlayersPlayerAttribute();
                        attr.name = pair.Key;
                        attr.Value = pair.Value;
                        attrs.Add(attr);
                    }
                    item.attribute = attrs.ToArray();

                    entries.Add(item);
                }
            }

            index.Item.player = entries.ToArray();

            using (ReplacingStream stream = new ReplacingStream(
                Path.Combine(Properties.Settings.Default.RealmDataPath,
                             Properties.Settings.Default.PlayersFileName)))
            {
                XmlSerializer ser = new XmlSerializer(typeof(XML.playerIndex));
                ser.Serialize(stream, index);
            }
        }

        public NetworkPlayer FindPlayer(string name)
        {
            NetworkPlayer result;
            players.TryGetValue(name.ToLower(), out result);
            return result;
        }

        public string GetPasswordSalt(string name)
        {
            NetworkPlayer who = FindPlayer(name);
            if (who == null)
                return null;
            else
                return who.PasswordSalt;
        }

        public NetworkPlayer ValidateLogIn(string name, string pwdSalt, string pwdHash)
        {
            NetworkPlayer player = FindPlayer(name);
            if (player == null || player.IsGuest)
                return null;

#if COVERUP
            if (!player.IsAdmin)
                return null;
#endif

            string correctSalt = player.PasswordSalt;
            string correctHash = player.PasswordHash;

            if (correctHash != null && (pwdSalt != correctSalt || pwdHash != correctHash))
                return null;

            return player;
        }

        public NetworkPlayer ValidateLogIn(string name, string password)
        {
            string salt = GetPasswordSalt(name);
            string hash = Controller.HashPassword(salt, password);
            return ValidateLogIn(name, salt, hash);
        }

        public void Run()
        {
            try
            {
                TcpListener listener = new TcpListener(System.Net.IPAddress.Any, port);

                LogMessage(LogLevel.Notice, "Listening on port {0}.", port);
                listener.Start();

                running = true;
                eventThread.Start();

                while (running)
                {
                    listener.BeginAcceptTcpClient(AcceptClientProc, listener);
                    mainLoopEvent.WaitOne();
                }

                eventThread.Join();

                lock (realms)
                    foreach (Instance r in instances.Values)
                        r.PolitelyDispose();
            }
            catch (Exception ex)
            {
                LogException(ex);
                throw;
            }
        }

        private void AcceptClientProc(IAsyncResult ar)
        {
            TcpListener listener = (TcpListener)ar.AsyncState;
            TcpClient client = listener.EndAcceptTcpClient(ar);

            string ip = FormatEndPoint(client.Client.RemoteEndPoint);
            LogMessage(LogLevel.Verbose, "Accepting connection from {0}.", ip);

            Thread clientThread = new Thread(new ParameterizedThreadStart(ClientThreadProc));
            clientThread.IsBackground = true;
            clientThread.Name = "Client: " + ip;
            clientThread.Start(client);

            mainLoopEvent.Set();
        }

        public void Shutdown(string reason)
        {
            LogMessage(LogLevel.Notice, "Shutting down: " + reason);

            string msg = "*** The server is shutting down immediately (" + reason + ") ***";

            lock (connections)
                foreach (Connection c in connections)
                {
                    c.WriteLine(msg);
                    c.FlushOutput();
                }

            running = false;
            mainLoopEvent.Set();
        }

        private static string FormatEndPoint(EndPoint endPoint)
        {
            IPEndPoint iep = endPoint as IPEndPoint;
            if (iep != null)
                return string.Format("{0}:{1}", iep.Address, iep.Port);
            else
                return "<not an IP endpoint>";
        }

#if COVERUP
        private Player MakeNewGuest()
        {
            lock (players)
            {
                int i = 1;
                bool inuse;
                string name;

                do
                {
                    name = "Guest" + i.ToString();
                    inuse = players.ContainsKey(name.ToLower());
                    i++;
                }
                while (inuse);

                Player result = new Player(100, name, false);
                players.Add(name.ToLower(), result);
                return result;
            }
        }

        private Realm MakeNewRealm()
        {
            string startRealm = Properties.Settings.Default.StartRealmName;

            lock (realms)
            {
                int i = 1;
                string newName;
                do
                {
                    newName = startRealm + "_" + i.ToString();
                    i++;
                } while (GetRealm(newName) != null);

                Realm origRealm = GetRealm(startRealm);
                Realm newRealm = new Realm(origRealm, newName);

                realms.Add(newName.ToLower(), newRealm);
                return newRealm;
            }
        }
#endif

        private void ClientThreadProc(object clientObj)
        {
            try
            {
                TcpClient client = (TcpClient)clientObj;
                Connection conn = new Connection(client);

                EndPoint otherSide = client.Client.RemoteEndPoint;

                lock (connections)
                    connections.Add(conn);

#if COVERUP
            conn.Player = MakeNewGuest();
            conn.Player.Connection = conn;
            Realm newRealm = MakeNewRealm();
            EnterRealm(conn.Player, newRealm);
#else
                GreetClient(conn);
#endif

                string line;
                while ((line = conn.ReadLine()) != null)
                {
                    // strip control characters and leading/trailing whitespace, and handle backspace
                    StringBuilder sb = new StringBuilder(line.Length);
                    foreach (char c in line)
                    {
                        if (char.IsWhiteSpace(c) && sb.Length == 0)
                            continue;

                        if (c == 8 && sb.Length > 0)
                            sb.Remove(sb.Length - 1, 1);
                        else if (c >= 32)
                            sb.Append(c);
                    }
                    while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1]))
                        sb.Length--;
                    line = sb.ToString();

                    GameInstance instance = null;
                    if (conn.Player != null)
                        lock (conn.Player)
                            instance = conn.Player.Instance;

                    if (instance == null)
                    {
#if COVERUP
                    conn.WriteLine("Unknown command.");
#else
                        // only handle out-of-realm commands (connect, create, quit, who)
                        if (!HandleSystemCommand(conn, ref line))
                        {
                            conn.WriteLine("Unknown command.");
                            conn.WriteLine();
                            GreetClient(conn);
                        }
#endif
                    }
#if !COVERUP
                    else if (HandleSystemCommand(conn, ref line))
                    {
                        // go on to the next line
                        continue;
                    }
#endif
                    else
                    {
                        string dabString;
                        lock (conn.Player)
                        {
                            dabString = conn.Player.Disambiguating;
                            conn.Player.Disambiguating = null;
                        }
                        if (dabString != null)
                        {
                            // repeat the previous command, but hide its output (the disambiguation question)
                            instance.QueueInput(conn.Player, dabString, true);
                            // provide the answer
                            instance.QueueInput(line);
                        }
                        else
                        {
#if !COVERUP
                            string lower = line.ToLower();

                            // handle special forms
                            if (line.StartsWith("\""))
                                line = "$say " + Sanitize(line.Substring(1));
                            else if (lower.StartsWith("say "))
                                line = "$say " + Sanitize(line.Substring(4));
                            else if (line.StartsWith(":"))
                                line = "$emote " + Sanitize(line.Substring(1));
                            else if (lower.StartsWith("pose "))
                                line = "$emote " + Sanitize(line.Substring(5));
                            else if (lower.StartsWith("emote "))
                                line = "$emote " + Sanitize(line.Substring(6));
                            else if (line.StartsWith(".."))
                            {
                                string[] parts = line.Substring(2).Split(new char[] { ' ' }, 2);
                                if (parts.Length < 2)
                                    line = Sanitize(line);
                                else if (parts[1].StartsWith(":"))
                                    line = "$emote >" + parts[0] + " " + Sanitize(parts[1].Substring(1));
                                else
                                    line = "$say >" + parts[0] + " " + Sanitize(parts[1]);
                            }
                            else
                                line = Sanitize(line);
#endif

                            // pass the line into the realm
                            instance.QueueInput(conn.Player, line, false);
                        }
                    }
                }

                LogMessage(LogLevel.Verbose, "Lost connection to {0}.", FormatEndPoint(otherSide));

                if (conn.Player != null)
                {
                    lock (conn.Player)
                        conn.Player.Connection = null;

#if COVERUP
                Realm realm;
                lock (conn.Player)
                    realm = conn.Player.Realm;

                if (realm != null)
                    realm.Deactivate();
#else
                    EnterInstance(conn.Player, null);
#endif

#if COVERUP
                    lock (players)
                        players.Remove(conn.Player.Name.ToLower());
#else
                    if (conn.Player.IsGuest)
                        lock (players)
                            players.Remove(conn.Player.Name.ToLower());
#endif

                    conn.Player = null;
                }

                lock (connections)
                    connections.Remove(conn);

                client.Close();
            }
            catch (Exception ex)
            {
                LogException(ex);
                throw;
            }
        }

        private void QueueEvent(VoidDelegate del)
        {
            lock (eventQueue)
            {
                eventQueue.Enqueue(del);
                if (eventQueue.Count == 1)
                    Monitor.Pulse(eventQueue);
            }
        }

        public void InstanceFinished(Instance instance, Player[] abandonedPlayers, bool wasTerminated)
        {
            QueueEvent(delegate { HandleInstanceFinished(instance, abandonedPlayers, wasTerminated); });
        }

        private void HandleInstanceFinished(Instance instance, Player[] abandonedPlayers, bool wasTerminated)
        {
#if COVERUP
            lock (realms)
            {
                // the realm might have been replaced while we were waiting for the lock...
                Realm storedRealm = GetRealm(realm.Name);
                if (storedRealm != null && storedRealm == realm)
                {
                    LogMessage(LogLevel.Verbose, "Removing realm '{0}' (finished).", realm.Name);
                    realms.Remove(realm.Name.ToLower());
                }
            }
#else // !COVERUP
            if (wasTerminated && !instance.RestartRequested)
            {
                LogMessage(LogLevel.Verbose, "Realm terminated: '{0}'", instance.Name);
            }
            else
            {
                // TODO: look at how long it's been since the realm was activated, and don't restart it if we suspect it's buggy

                instance.RestartRequested = false;
                LogMessage(LogLevel.Verbose, "Restarting realm '{0}'", instance.Name);

                instance.Activate();
            }
#endif

            GameInstance initialRealm = GetDefaultInstance(GetRealm(Properties.Settings.Default.StartRealmName));

            foreach (Player p in abandonedPlayers)
            {
                lock (p)
                {
                    p.Instance = null;
#if COVERUP
                    if (p.Connection != null)
                        p.Connection.Terminate();
#else
                    EnterInstance(p, initialRealm);
#endif
                }
            }
        }

        private void HandleInstanceFailure(Instance r, string reason)
        {
            bool isCondemned = r.Realm.IncrementFailureCount();

            if (isCondemned)
            {
                LogMessage(LogLevel.Warning, "Realm '{0}' failed too many times and is now condemned", r.Name);
            }
            else
            {
                LogMessage(LogLevel.Warning, "Realm '{0}' failed but will be restarted", r.Name);
                r.RestartRequested = true;
            }

            SetEventInterval(r, 0);
            r.Deactivate();
        }

        private static string GetToken(ref string str, char delim)
        {
            string result;
            int idx = str.IndexOf(delim);

            if (idx >= 0)
            {
                result = str.Substring(0, idx);
                str = str.Substring(idx + 1);
            }
            else
            {
                result = str;
                str = "";
            }

            return result;
        }

        private void EnterInstance(Player player, GameInstance realm)
        {
            EnterInstance(player, realm, null, false);
        }

        private void EnterInstance(Player player, GameInstance realm, string position)
        {
            EnterInstance(player, realm, position, false);
        }

        private void EnterInstance(Player player, GameInstance instance, string position, bool traveling)
        {
            GameInstance prevRealm;
            lock (player)
            {
                prevRealm = player.Instance;
                if (!traveling)
                    player.SetAttribute("waygone", null);
            }

            if (prevRealm == instance)
                return;

            if (prevRealm != null)
            {
                LogMessage(LogLevel.Verbose,
                    "{0} leaving '{1}'.",
                    player.LogName,
                    prevRealm.Name);

                prevRealm.RemovePlayer(player);
            }

            lock (player)
                player.Instance = instance;

            if (instance != null)
            {
                LogMessage(LogLevel.Verbose,
                    "{0} entering '{1}'.",
                    player.LogName,
                    instance.Name);

                if (!instance.IsActive)
                    instance.Activate();

                instance.AddPlayer(player, position);
            }
        }

        public void TransferPlayer(Player player, string spec)
        {
            QueueEvent(delegate { HandleTransferPlayer(player, spec); });
        }

        private void HandleTransferPlayer(Player player, string spec)
        {
            string instanceName, token;

            int idx = spec.IndexOf('@');
            if (idx == -1)
            {
                instanceName = spec;
                token = "default";
            }
            else
            {
                instanceName = spec.Substring(idx + 1);
                token = spec.Substring(0, idx);
            }

            GameInstance dest = GetInstance(instanceName) as GameInstance;

            if (dest == null)
            {
                Realm realm = GetRealm(instanceName);
                if (realm != null)
                    dest = GetDefaultInstance(realm);
            }

            if (dest != null)
            {
                if (dest == player.Instance)
                {
                    TransferError(player, instanceName, "you're already there");
                }
                else if (dest.Realm.IsCondemned)
                {
                    TransferError(player, instanceName, "that realm has been condemned");
                }
                else if (dest.Realm.GetAccessLevel(player) <= RealmAccessLevel.Banned)
                {
                    TransferError(player, instanceName, "you aren't allowed to enter that realm");
                }
                else
                {
                    dest.Activate();

                    // TODO: let the destination know which realm is trying to send a player, so it can accept only from certain source realms
                    string check = dest.SendAndGet("$knock " + token);
                    switch (check)
                    {
                        case "ok":
                            EnterInstance(player, dest, "=" + token, true);
                            break;

                        case "full":
                            TransferError(player, instanceName, "that realm is full");
                            break;

                        case "invalid":
                            TransferError(player, instanceName,
                                "that realm has no entrance called \"" + token + "\"");
                            break;

                        default:
                            TransferError(player, instanceName,
                                "it failed mysteriously (\"" + check + "\")");
                            break;
                    }
                }
            }
            else
            {
                TransferError(player, instanceName, "there is no such place");
            }
        }

        private static void TransferError(Player player, string realmName, string message)
        {
            player.WriteLine("*** This realm tried to send you to \"{0}\", but {1}. ***",
                realmName, message);
            player.FlushOutput();
        }

        public void DisconnectPlayer(Player bot)
        {
            QueueEvent(delegate { EnterInstance(bot, null); });
        }

        private void ShowWhoList(Connection conn, Player player)
        {
            conn.WriteLine("{0,-25} {1,-6} {2,-6} {3}", "Player", "Conn", "Idle", "Realm");

            int count = 0;
            lock (connections)
            {
                for (int i = connections.Count - 1; i >= 0; i--)
                {
                    Connection c = connections[i];

                    Player p;
                    TimeSpan connectedTime, idleTime;
                    lock (c)
                    {
                        p = c.Player;
                        connectedTime = c.ConnectedTime;
                        idleTime = c.IdleTime;
                    }

                    if (p != null)
                    {
                        count++;

                        Realm r;
                        lock (p)
                            r = p.Realm;

                        string realmName;
                        if (r == null)
                            realmName = "<none>";
                        else if (r.GetAccessLevel(player) < RealmAccessLevel.Visible)
                            realmName = "<private>";
                        else
                            realmName = r.Name;

                        conn.WriteLine("{0,-25} {1,-6} {2,-6} {3}",
                            p.Name,
                            FormatTimeSpan(connectedTime),
                            FormatTimeSpan(idleTime),
                            realmName);
                    }
                }
            }

            conn.WriteLine("{0} player{1} connected.", count, count == 1 ? "" : "s");
        }

        public string[][] GetWhoList()
        {
            lock (connections)
            {
                List<string[]> result = new List<string[]>(connections.Count);

                foreach (Connection c in connections)
                {
                    if (c.Player != null)
                    {
                        result.Add(new string[]{
                            c.Player.Name,
                            FormatTimeSpan(c.ConnectedTime),
                            FormatTimeSpan(c.IdleTime)
                        });
                    }
                }

                result.Reverse();
                return result.ToArray();
            }
        }

        public static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.Days >= 1)
                return string.Format("{0:00}d{1:00}h", timeSpan.Days, timeSpan.Hours);
            else if (timeSpan.Hours >= 1)
                return string.Format("{0:00}h{1:00}m", timeSpan.Hours, timeSpan.Minutes);
            else
                return string.Format("{0:00}m{1:00}s", timeSpan.Minutes, timeSpan.Seconds);
        }

        public static string Sanitize(string str)
        {
            // sanitize input
            StringBuilder sb = new StringBuilder(str.Length);
            foreach (char c in str)
            {
                switch (c)
                {
                    case '$': sb.Append("&dollar;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '&': sb.Append("&amp;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        public static string Desanitize(string str)
        {
            // reverse sanitization
            StringBuilder sb = new StringBuilder(str.Length);
            int i = -1, j = -1;
            do
            {
                i = str.IndexOf('&', j + 1);
                if (i >= 0)
                {
                    sb.Append(str.Substring(j + 1, i - j - 1));
                    j = str.IndexOf(';', i + 1);
                    if (j >= 0)
                    {
                        switch (str.Substring(i + 1, j - i - 1))
                        {
                            case "dollar": sb.Append('$'); break;
                            case "lt": sb.Append('<'); break;
                            case "gt": sb.Append('>'); break;
                            case "amp": sb.Append('&'); break;
                            default: sb.Append(str.Substring(i, j - i + 1)); break;
                        }
                    }
                    else
                    {
                        // & with no terminating ;
                        sb.Append(str.Substring(i));
                        break;
                    }
                }
                else
                {
                    // no more &
                    sb.Append(str.Substring(j + 1));
                }
            } while (i >= 0);
            return sb.ToString();
        }

        private void GreetClient(Connection conn)
        {
            string file = Path.Combine(
                Properties.Settings.Default.RealmDataPath,
                Properties.Settings.Default.GreetingFileName);

            if (File.Exists(file))
            {
                try
                {
                    conn.Write(File.ReadAllText(file));
                    return;
                }
                catch
                {
                    // use default greeting
                }
            }

            // default greeting
            conn.WriteLine("Welcome to the server!");
            conn.WriteLine("Type \"connect <name> <password>\" to connect.");
            conn.WriteLine("To create a new character, use the web site: " + Properties.Settings.Default.WebAddress);
        }

        private void SendTextFile(Connection conn, string playerName, string fileName)
        {
            string path = Path.Combine(
                Properties.Settings.Default.RealmDataPath,
                fileName);

            if (File.Exists(path))
            {
                try
                {
                    string text = File.ReadAllText(path);
                    text = text.Replace("%NAME%", playerName);
                    conn.Write(text);
                }
                catch
                {
                    // no motd for you!
                }
            }
        }
    }
}