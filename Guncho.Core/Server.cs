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

using Guncho.Api;
using Guncho.Api.Security;
using Guncho.Connections;
using Guncho.Services;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.Hosting.Services;
using Microsoft.Owin.Hosting.Starter;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Thinktecture.IdentityModel.Owin.ResourceAuthorization;

using IWebDependencyResolver = System.Web.Http.Dependencies.IDependencyResolver;
using ISignalRDependencyResolver = Microsoft.AspNet.SignalR.IDependencyResolver;
using Microsoft.Owin.Security.DataProtection;
using Microsoft.Owin.Security;
using Nito.AsyncEx;

namespace Guncho
{
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

    public partial class Server : IRealmsService, IPlayersService
    {
        private readonly ServerConfig config;
        private readonly ILogger logger;
        private readonly IWebDependencyResolver apiDependencyResolver;
        private readonly ISignalRDependencyResolver sigrDependencyResolver;
        private readonly ISignalRConnectionManager sigrManager;
        private readonly IDataProtectionProvider dataProtectionProvider;
        private readonly ISecureDataFormat<AuthenticationTicket> oauthTicketFormat;

        private volatile bool running;
        private TaskCompletionSource<bool> whenShutDown;

        private readonly ConcurrentDictionary<Connection, Task> openConnections = new ConcurrentDictionary<Connection, Task>();
        private readonly ConcurrentDictionary<string, Player> players = new ConcurrentDictionary<string, Player>();
        private readonly ConcurrentDictionary<int, Player> playersById = new ConcurrentDictionary<int, Player>();
        private readonly ConcurrentDictionary<string, Realm> realms = new ConcurrentDictionary<string, Realm>();
        private readonly ConcurrentDictionary<string, Instance> instances = new ConcurrentDictionary<string, Instance>();
        private readonly ConcurrentDictionary<string, RealmFactory> factories = new ConcurrentDictionary<string, RealmFactory>();
        private readonly AsyncProducerConsumerQueue<Func<Task>> eventQueue = new AsyncProducerConsumerQueue<Func<Task>>();
        private Task eventTask;
        private readonly PriorityQueue<TimedEvent> timedEvents = new PriorityQueue<TimedEvent>();
        private readonly ConcurrentDictionary<Instance, TimedEvent> timedEventsByInstance = new ConcurrentDictionary<Instance, TimedEvent>();
        //private readonly AutoResetEvent mainLoopEvent = new AutoResetEvent(false);

        public IResourceAuthorizationManager ResourceAuthorizationManager { get; set; }

        public Server(ServerConfig config, ILogger logger,
            IWebDependencyResolver apiDependencyResolver,
            ISignalRDependencyResolver sigrDependencyResolver,
            ISignalRConnectionManager sigrManager,
            IEnumerable<RealmFactory> allRealmFactories,
            IDataProtectionProvider dataProtectionProvider,
            ISecureDataFormat<AuthenticationTicket> oauthTicketFormat)
        {
            if (logger == null)
                throw new ArgumentNullException("logger");

            this.config = config;
            this.logger = logger;
            this.apiDependencyResolver = apiDependencyResolver;
            this.sigrDependencyResolver = sigrDependencyResolver;
            this.sigrManager = sigrManager;
            this.dataProtectionProvider = dataProtectionProvider;
            this.oauthTicketFormat = oauthTicketFormat;

            try
            {
                Task.WaitAll(LoadPlayers(), LoadRealms(allRealmFactories));

                if (GetRealm(Properties.Settings.Default.StartRealmName) == null)
                    throw new InvalidOperationException(
                        string.Format("Couldn't load initial realm '{0}'",
                                        Properties.Settings.Default.StartRealmName));
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
                throw;
            }
        }

        private async Task EventTaskProc()
        {
            try
            {
                int granularity = Properties.Settings.Default.EventGranularity;
                Task<Func<Task>> getQueuedEvent = eventQueue.DequeueAsync();

                while (running)
                {
                    var task = await Task.WhenAny(getQueuedEvent, Task.Delay(granularity));

                    if (task == getQueuedEvent)
                    {
                        var eventToRun = await getQueuedEvent;
                        await eventToRun();
                        getQueuedEvent = eventQueue.DequeueAsync();
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
                logger.LogException(ex);
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

            foreach (Instance r in instances.Values)
            {
                if (now >= r.WatchdogTime)
                {
                    Instance failed = r;
                    QueueEvent(() => HandleInstanceFailure(failed, "frozen"));
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
                    TimedEvent dummy;
                    timedEventsByInstance.TryRemove(instance, out dummy);
                }
                else
                {
                    TimedEvent ev = new TimedEvent(instance, seconds);
                    timedEventsByInstance[instance] = ev;
                    timedEvents.Enqueue(ev, ev.Time.ToFileTime());
                }
            }
        }

        private async Task LoadRealms(IEnumerable<RealmFactory> allRealmFactories)
        {
            // build factories dict first
            string defaultFactory = null;

            factories.Clear();

            foreach (var factory in allRealmFactories)
            {
                var name = factory.Name;

                if (defaultFactory == null || name.CompareTo(defaultFactory) < 0)
                    defaultFactory = name;

                factories.TryAdd(name, factory);
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
            realms.Clear();
            foreach (XML.realmIndexRealm entry in index.realms)
            {
                Player owner = GetPlayerByName(entry.owner);
                try
                {
                    await LoadRealmAsync(entry.name, Path.Combine(dataPath, entry.src),
                        entry.factory ?? defaultFactory, owner);
                }
                catch (RealmLoadingException rle)
                {
                    logger.LogMessage(LogLevel.Error,
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
                            Player p = GetPlayerByName(xent.player);
                            if (p == null)
                            {
                                logger.LogMessage(LogLevel.Warning,
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
                                    logger.LogMessage(LogLevel.Warning,
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

        public Task SaveRealms()
        {
            XML.realmIndex index = new Guncho.XML.realmIndex();
            List<XML.realmIndexRealm> entries = new List<Guncho.XML.realmIndexRealm>();

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

            index.realms = entries.ToArray();

            using (ReplacingStream stream = new ReplacingStream(
                Path.Combine(Properties.Settings.Default.RealmDataPath,
                             Properties.Settings.Default.RealmsFileName)))
            {
                XmlSerializer ser = new XmlSerializer(typeof(XML.realmIndex));
                ser.Serialize(stream, index);
            }

            return TaskConstants.Completed;
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
        public Task<RealmEditingOutcome> LoadRealmAsync(string realmName, string sourceFile, string factoryName,
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

#if COVERUP
                string cachedFile = Path.Combine(Properties.Settings.Default.CachePath, realmName + ".ulx");
                if (!File.Exists(cachedFile))
                    return;
#else
                if (!File.Exists(sourceFile))
                {
                    logger.LogMessage(LogLevel.Error,
                        "Skipping realm '{0}': source file '{1}' missing",
                        realmName, sourceFile);
                    return Task.FromResult(RealmEditingOutcome.Missing);
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
                    logger.LogMessage(LogLevel.Verbose,
                        "Removing realm '{0}' (failed to compile in LoadRealm).", realmName);
                    Realm dummy;
                    realms.TryRemove(realmName, out dummy);
                    return Task.FromResult(outcome);
                }
#endif

            //FileStream stream = new FileStream(cachedFile, FileMode.Open, FileAccess.Read);
            try
            {
                Realm r = factory.LoadRealm(this, realmName, sourceFile, cachedFile, owner);
#if COVERUP
                    r.RawMode = true;
#endif
                realms.TryAdd(realmName.ToLower(), r);
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
                    //stream.Close();
                    throw new RealmLoadingException(realmName, ex);
                }
            }

            return Task.FromResult(RealmEditingOutcome.Success);
        }

        private Task<Instance> LoadInstance(Realm realm, string name)
        {
            Instance result;

            if (GetInstance(name) != null)
                throw new ArgumentException("An instance with this name is already loaded", "name");
            result = realm.Factory.LoadInstance(this, realm, name, logger);
            return Task.FromResult(instances.GetOrAdd(name.ToLower(), result));
        }

        public Realm GetRealm(string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            Realm result;
            realms.TryGetValue(name.ToLower(), out result);
            return result;
        }

        public Instance GetInstance(string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            Instance result;
            instances.TryGetValue(name.ToLower(), out result);
            return result;
        }

        private async Task<Instance> GetDefaultInstance(Realm realm)
        {
            Instance inst = GetInstance(realm.Name);

            if (inst != null)
                return inst;

            try
            {
                return await LoadInstance(realm, realm.Name);
            }
            catch (ArgumentException)
            {
                return GetInstance(realm.Name);
            }
        }

        private Instance[] GetAllInstances(Realm realm)
        {
            List<Instance> result = new List<Instance>();

            return instances.Values.Where(inst => inst.Realm == realm).ToArray();
        }

        #region IRealmsService

        public IEnumerable<Realm> GetAllRealms()
        {
            return realms.Values.ToArray();
        }

        public IEnumerable<RealmFactory> GetRealmFactories()
        {
            return factories.Values;
        }

        public Realm GetRealmByName(string name)
        {
            Realm result;
            realms.TryGetValue(name.ToLower(), out result);
            return result;
        }

        public async Task<Realm> CreateRealm(Player newOwner, string newName, RealmFactory factory)
        {
#if COVERUP
            // no realm creation in coverup mode
            return null;
#else
            string key = newName.ToLower();

            if (!IsValidRealmName(newName))
                return null;

            if (realms.ContainsKey(key))
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
            await FileAsync.WriteAllText(source, factory.GetInitialSourceText(newOwner.Name, newName));
            try
            {
                await LoadRealmAsync(newName, source, factory.Name, newOwner);
            }
            catch
            {
                return null;
            }
            await SaveRealms();
            return GetRealm(newName);
#endif
        }

        public async Task<RealmEditingOutcome> UpdateRealmSourceAsync(Realm realm, Stream newSource)
        {
            // TODO: enforce access controls (ownership, condemned realms)
            // TODO: record the player name

            logger.LogMessage(LogLevel.Verbose, "CP: changing source of '{0}'.", realm.Name);

            string previewName = realm.Name + ".preview";

            string tempFile = Path.GetTempFileName();
            try
            {
                using (var tempStream = File.OpenWrite(tempFile))
                {
                    await newSource.CopyToAsync(tempStream);
                }
                try
                {
                    var outcome = await LoadRealmAsync(previewName, tempFile, realm.Factory.Name, realm.Owner);

                    if (outcome != RealmEditingOutcome.Success)
                        return outcome;
                }
                catch (RealmLoadingException)
                {
                    return RealmEditingOutcome.VMError;
                }

                // successfully changed
                await ReplaceRealm(previewName, realm.Name);
                return RealmEditingOutcome.Success;
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public async Task<bool> TransactionalUpdate(Realm realm, Func<Realm, bool> transaction)
        {
            bool success;

            using (await realm.Lock.WriterLockAsync())
            {
                success = transaction(realm);
            }

            if (success)
            {
                await SaveRealms();
            }

            return success;
        }

        #endregion

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
        public async Task ReplaceRealm(string fromName, string toName)
        {
            if (fromName == null)
                throw new ArgumentNullException("fromName");
            if (toName == null)
                throw new ArgumentNullException("toName");

            logger.LogMessage(LogLevel.Spam, "Renaming realm '{0}' to '{1}'.", fromName, toName);

            if (fromName.ToLower() == toName.ToLower())
                return;

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
            Instance dummyInstance;

            foreach (Instance inst in origInstances)
            {
                var dict = new Dictionary<Player, string>();
                saved.Add(inst.Name, dict);
                await inst.ExportPlayerPositions(dict);
                SetEventInterval(inst, 0);
                inst.PolitelyDispose();

                instances.TryRemove(inst.Name.ToLower(), out dummyInstance);
            }

            // there shouldn't be any players in replacement instances, but
            // if there are for some reason, dump them in the new default instance
            foreach (Instance inst in replcInstances)
            {
                Dictionary<Player, string> dict;
                if (saved.TryGetValue(toName, out dict) == false)
                {
                    dict = new Dictionary<Player, string>();
                    saved.Add(toName, dict);
                }
                await inst.ExportPlayerPositions(dict);
                SetEventInterval(inst, 0);
                inst.Dispose();
                instances.TryRemove(inst.Name.ToLower(), out dummyInstance);
            }

            logger.LogMessage(LogLevel.Spam, "Removing realms '{0}' and '{1}'.", fromName, toName);
            Realm dummy;
            realms.TryRemove(fromName.ToLower(), out dummy);
            realms.TryRemove(toName.ToLower(), out dummy);

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

            string fromIndex = Path.Combine(config.IndexPath, fromName);
            string toIndex = Path.Combine(config.IndexPath, toName);
            if (Directory.Exists(fromIndex))
                RealmFactory.CopyDirectory(fromIndex, toIndex);

            // reload realm
            await LoadRealmAsync(toName, toSource, replacement.Factory.Name, replacement.Owner);
            Realm newRealm = GetRealm(toName);
            if (newRealm != null)
            {
                newRealm.CopySettingsFrom(original);

                logger.LogMessage(LogLevel.Verbose, "Reloaded '{0}'.", newRealm.Name);

                foreach (var instPair in saved)
                {
                    Instance inst = await LoadInstance(newRealm, instPair.Key);
                    foreach (KeyValuePair<Player, string> pair in instPair.Value)
                    {
                        using (await pair.Key.Lock.ReaderLockAsync())
                        {
                            if (pair.Key.Connection != null)
                                await pair.Key.Connection.WriteLineAsync("[The realm shimmers for a moment...]");
                        }
                        await EnterInstance(pair.Key, inst, pair.Value);
                    }
                }
            }
            else
            {
                logger.LogMessage(LogLevel.Error, "Failed to reload '{0}' in ReplaceRealm.", toName);

                Instance startInst = await GetDefaultInstance(GetRealm(Properties.Settings.Default.StartRealmName));
                foreach (Dictionary<Player, string> positions in saved.Values)
                {
                    foreach (KeyValuePair<Player, string> pair in positions)
                    {
                        using (await pair.Key.Lock.ReaderLockAsync())
                        {
                            if (pair.Key.Connection != null)
                                await pair.Key.Connection.WriteLineAsync("[The realm has failed.]");
                        }
                        await EnterInstance(pair.Key, startInst, pair.Value);
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

        public async Task<Player> CreatePlayer(string newName, string pwdSalt, string pwdHash)
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

            string key = newName.ToLower();
            if (!players.TryAdd(key, null))
                return null;

            // find an unused ID
            int id = 1;
            while (true)
            {
                if (playersById.TryAdd(id, null))
                    break;

                id++;
            }

            Player result = new Player(id, newName, false)
            {
                PasswordSalt = pwdSalt,
                PasswordHash = pwdHash,
            };

            if (!players.TryUpdate(key, result, null) || !playersById.TryUpdate(id, result, null))
                throw new InvalidOperationException("Another task stole our player ID or name");

            await SavePlayers();
            return result;
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

        public async Task<bool> DeleteRealm(Realm realm)
        {
#if COVERUP
            // no realm deletion in coverup mode
            return false;
#else
            Realm startRealm = GetRealm(Properties.Settings.Default.StartRealmName);

            if (realm == startRealm)
                return false;

            logger.LogMessage(LogLevel.Notice, "Deleting realm '{0}'", realm.Name);
            Realm dummy;
            realms.TryRemove(realm.Name.ToLower(), out dummy);

            // close all instances
            Instance startInstance = await GetDefaultInstance(startRealm);
            foreach (Instance inst in GetAllInstances(realm))
            {
                if (inst.IsActive)
                {
                    await Task.WhenAll(from p in inst.ListPlayers()
                                       select EnterInstance(p, startInstance));
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
                string index = Path.Combine(config.IndexPath, realm.Name);
                Directory.Delete(index, true);
            }
            catch (IOException) { }

            await SaveRealms();
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

        private Task LoadPlayers()
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
            playersById.Clear();

            foreach (XML.playerIndexPlayersPlayer entry in index.Item.player)
            {
                Player player = new Player(entry.id, entry.name,
                    entry.adminSpecified && entry.admin);
                player.PasswordSalt = entry.pwdSalt;
                player.PasswordHash = entry.pwdHash;
                players.TryAdd(player.Name.ToLower(), player);
                playersById.TryAdd(player.ID, player);

                if (entry.attribute != null)
                    foreach (XML.playerIndexPlayersPlayerAttribute attr in entry.attribute)
                        player.SetAttribute(attr.name, attr.Value);
            }

            return TaskConstants.Completed;
        }

        public Task SavePlayers()
        {
            XML.playerIndex index = new Guncho.XML.playerIndex();
            index.Item = new Guncho.XML.playerIndexPlayers();
            List<XML.playerIndexPlayersPlayer> entries = new List<Guncho.XML.playerIndexPlayersPlayer>();

            foreach (Player p in players.Values)
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

            index.Item.player = entries.ToArray();

            using (ReplacingStream stream = new ReplacingStream(
                Path.Combine(Properties.Settings.Default.RealmDataPath,
                             Properties.Settings.Default.PlayersFileName)))
            {
                XmlSerializer ser = new XmlSerializer(typeof(XML.playerIndex));
                ser.Serialize(stream, index);
            }

            return TaskConstants.Completed;
        }

        #region IPlayersService

        public IEnumerable<Player> GetAllPlayers()
        {
            return players.Values.ToArray();
        }
        
        public Player GetPlayerByName(string name)
        {
            Player result;
            players.TryGetValue(name.ToLower(), out result);
            return result;
        }

        public Player GetPlayerById(int id)
        {
            Player result;
            playersById.TryGetValue(id, out result);
            return result;
        }

        bool IPlayersService.IsValidNameChange(string oldName, string newName)
        {
            if (oldName.ToLower() == newName.ToLower())
            {
                return true;
            }

            return PlayersServiceConstants.UserNameRegex.IsMatch(newName) && GetPlayerByName(newName) == null;
        }

        bool IRealmsService.IsValidNameChange(string oldName, string newName)
        {
            if (oldName.ToLower() == newName.ToLower())
            {
                return true;
            }

            return newName.Trim() == newName && GetRealmByName(newName) == null;
        }
        
        public async Task<bool> TransactionalUpdate(Player player, Func<Player, bool> transaction)
        {
            bool success;

            using (await player.Lock.WriterLockAsync())
            {
                success = transaction(player);
            }

            if (success)
            {
                await SavePlayers();
            }

            return success;
        }

        #endregion

        public string GetPasswordSalt(string name)
        {
            Player who = GetPlayerByName(name);
            if (who == null)
                return null;
            else
                return who.PasswordSalt;
        }

        public Player ValidateLogIn(string name, string pwdSalt, string pwdHash)
        {
            Player player = GetPlayerByName(name);
            if (player == null || player.IsGuest)
                return null;

#if COVERUP
            if (!player.IsAdmin)
                return null;
#endif

            string correctSalt, correctHash;

            using (player.Lock.ReaderLock())
            {
                correctSalt = player.PasswordSalt;
                correctHash = player.PasswordHash;
            }

            if (correctHash != null && (pwdSalt != correctSalt || pwdHash != correctHash))
                return null;

            return player;
        }

        public Player ValidateLogIn(string name, string password)
        {
            string salt = GetPasswordSalt(name);
            string hash = OldTimeyPasswordHasher.HashPassword(salt, password);
            return ValidateLogIn(name, salt, hash);
        }

        private IDisposable StartWebApi()
        {
            var services = (ServiceProvider)ServicesFactory.Create();
            services.AddInstance<IWebDependencyResolver>(apiDependencyResolver);
            services.AddInstance<ISignalRDependencyResolver>(sigrDependencyResolver);
            services.AddInstance<IResourceAuthorizationManager>(ResourceAuthorizationManager);
            services.AddInstance<ISignalRConnectionManager>(sigrManager);
            services.AddInstance<IDataProtectionProvider>(dataProtectionProvider);
            services.AddInstance<ISecureDataFormat<AuthenticationTicket>>(oauthTicketFormat);

            //XXX
            // TODO: break this ugly dependency
            services.AddInstance<Microsoft.Owin.Security.OAuth.IOAuthAuthorizationServerProvider>(
                new GunchoOAuthServerProvider(
                    new Microsoft.AspNet.Identity.UserManager<ApiUser, int>(new OldTimeyUserStore(this))
                    {
                        PasswordHasher = new OldTimeyPasswordHasher(),
                    }));

            var options = new StartOptions() { Port = config.WebPort };
            options.AppStartup = typeof(WebApiStartup).AssemblyQualifiedName;

            logger.LogMessage(LogLevel.Notice, "Web: Listening on port {0}.", config.WebPort);

            return services.GetService<IHostingStarter>().Start(options);
        }

        public async Task RunAsync()
        {
            try
            {
                var cancellation = new CancellationTokenSource();

                running = true;
                whenShutDown = new TaskCompletionSource<bool>();
                eventTask = Task.Run(EventTaskProc);

                // TCP connections
                //XXX break dependency
                var tcpManager = new TcpConnectionManager(System.Net.IPAddress.Any, config.GamePort);
                tcpManager.ConnectionAccepted += (sender, e) =>
                {
                    logger.LogMessage(LogLevel.Verbose, "TCP: Accepting connection from {0}.", FormatEndPoint(e.Connection.OtherSide));
                    var connTask = HandleConnection(e.Connection, cancellation.Token);
                    openConnections.TryAdd(e.Connection, connTask);
                };
                tcpManager.ConnectionClosed += (sender, e) =>
                {
                    logger.LogMessage(LogLevel.Verbose, "TCP: Lost connection to {0}.", FormatEndPoint(e.Connection.OtherSide));
                    Task dummy;
                    openConnections.TryRemove(e.Connection, out dummy);
                };

                logger.LogMessage(LogLevel.Notice, "TCP: Listening on port {0}.", config.GamePort);
                var tcpListenTask = tcpManager.Run(cancellation.Token);

                // SignalR connections
                EventHandler<ConnectionAcceptedEventArgs<SignalRConnection>> sigrAcceptedHandler =
                    (sender, e) =>
                    {
                        logger.LogMessage(LogLevel.Verbose, "SignalR: Accepting connection with ID {0}.", e.Connection.ConnectionId);
                        var connTask = HandleConnection(e.Connection, cancellation.Token, e.AuthenticatedUserName);
                        openConnections.TryAdd(e.Connection, connTask);
                    };
                EventHandler<ConnectionEventArgs<SignalRConnection>> sigrClosedHandler =
                    (sender, e) =>
                    {
                        logger.LogMessage(LogLevel.Verbose, "SignalR: Lost connection with ID {0}.", e.Connection.ConnectionId);
                        Task dummy;
                        openConnections.TryRemove(e.Connection, out dummy);
                    };
                sigrManager.ConnectionAccepted += sigrAcceptedHandler;
                sigrManager.ConnectionClosed += sigrClosedHandler;

                logger.LogMessage(LogLevel.Notice, "SignalR: Listening.");  //XXX where?
                var sigrListenTask = sigrManager.Run(cancellation.Token);

                try
                {
                    using (StartWebApi())
                    {
                        await Task.WhenAny(tcpListenTask, sigrListenTask, whenShutDown.Task);
                        await Task.WhenAll(from connection in openConnections.Keys
                                           select connection.TerminateAsync());
                    }
                }
                finally
                {
                    sigrManager.ConnectionAccepted -= sigrAcceptedHandler;
                    sigrManager.ConnectionClosed -= sigrClosedHandler;
                }

                await eventTask;

                foreach (Instance r in instances.Values)
                    r.PolitelyDispose();
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
                throw;
            }
        }

        public async Task ShutdownAsync(string reason)
        {
            logger.LogMessage(LogLevel.Notice, "Shutting down: " + reason);

            string msg = "*** The server is shutting down immediately (" + reason + ") ***";

            foreach (Connection c in openConnections.Keys)
            {
                await c.WriteLineAsync(msg);
                await c.FlushOutputAsync();
            }

            running = false;
            whenShutDown.TrySetResult(true);
            //mainLoopEvent.Set();
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

        private static string RewriteChatCommandsIfNeeded(string line)
        {
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
            return line;
        }

        private static string TrimAndHandleBackspace(string line)
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
            return sb.ToString();
        }

        private static bool UnpackHandleSystemCommandResult(HandleSystemCommandResult result, out string line)
        {
            line = result.Line;
            return result.Handled;
        }

        private async Task HandleConnection(Connection conn, CancellationToken cancellationToken, string authenticatedUser = null)
        {
            if (authenticatedUser != null)
            {
                // log them in immediately
                if (authenticatedUser.ToLower() == "guest")
                {
                    await LogInAsGuest(conn);
                }
                else
                {
                    var player = GetPlayerByName(authenticatedUser);
                    if (player != null)
                    {
                        await LogInAsPlayer(conn, player);
                    }
                    else
                    {
                        // no player by that name? weird
                        logger.LogMessage(LogLevel.Error, "Can't auto-login authenticatedUser because they don't exist: {0}", authenticatedUser);
                        await GreetClientAsync(conn);
                    }
                }
            }
            else
            {
                // send the normal greeting
                await GreetClientAsync(conn);
            }

            string line;
            while ((line = await conn.ReadLineAsync(cancellationToken)) != null)
            {
                line = TrimAndHandleBackspace(line);

                Instance instance = null;
                if (conn.Player != null)
                {
                    using (await conn.Player.Lock.ReaderLockAsync())
                    {
                        instance = conn.Player.Instance;
                    }
                }

                if (instance == null)
                {
                    // only handle out-of-realm commands (connect, create, quit, who)
                    if (!UnpackHandleSystemCommandResult(await HandleSystemCommand(conn, line), out line))
                    {
                        await conn.WriteLineAsync("Unknown command.");
                        await conn.WriteLineAsync();
                        await GreetClientAsync(conn);
                    }
                }
                else if (UnpackHandleSystemCommandResult(await HandleSystemCommand(conn, line), out line))
                {
                    // go on to the next line
                    continue;
                }
                else
                {
                    string dabString;

                    using (await conn.Player.Lock.WriterLockAsync())
                    {
                        dabString = conn.Player.Disambiguating;
                        conn.Player.Disambiguating = null;
                    }

                    if (dabString != null)
                    {
                        // repeat the previous command, but hide its output (the disambiguation question)
                        instance.QueueInput(MakeInputLine(conn, dabString, true));
                        // provide the answer
                        instance.QueueInput(line);
                    }
                    else
                    {
                        line = RewriteChatCommandsIfNeeded(line);

                        // pass the line into the realm
                        instance.QueueInput(MakeInputLine(conn, line, false));
                    }
                }
            }

            // connection lost
            if (conn.Player != null)
            {
                using (await conn.Player.Lock.WriterLockAsync())
                {
                    conn.Player.Connection = null;
                }

                await EnterInstance(conn.Player, null);

                if (conn.Player.IsGuest)
                {
                    Player dummy;
                    players.TryRemove(conn.Player.Name.ToLower(), out dummy);
                    playersById.TryRemove(conn.Player.ID, out dummy);
                }

                conn.Player = null;
            }
        }

        private void QueueEvent(Func<Task> del)
        {
            eventQueue.Enqueue(del);
        }

        public void InstanceFinished(Instance instance, Player[] abandonedPlayers, bool wasTerminated)
        {
            QueueEvent(() => HandleInstanceFinished(instance, abandonedPlayers, wasTerminated));
        }

        private async Task HandleInstanceFinished(Instance instance, Player[] abandonedPlayers, bool wasTerminated)
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
                logger.LogMessage(LogLevel.Verbose, "Realm terminated: '{0}'", instance.Name);
            }
            else
            {
                // TODO: look at how long it's been since the realm was activated, and don't restart it if we suspect it's buggy

                instance.RestartRequested = false;
                logger.LogMessage(LogLevel.Verbose, "Restarting realm '{0}'", instance.Name);

                instance.Activate();
            }
#endif

            Instance initialRealm = await GetDefaultInstance(GetRealm(Properties.Settings.Default.StartRealmName));

            foreach (Player p in abandonedPlayers)
            {
                using (await p.Lock.WriterLockAsync())
                {
                    p.Instance = null;
#if COVERUP
                    if (p.Connection != null)
                        p.Connection.Terminate();
#else
                    await EnterInstance(p, initialRealm);
#endif
                }
            }
        }

        private Task HandleInstanceFailure(Instance r, string reason)
        {
            bool isCondemned = r.Realm.IncrementFailureCount();

            if (isCondemned)
            {
                logger.LogMessage(LogLevel.Warning, "Realm '{0}' failed too many times and is now condemned", r.Name);
            }
            else
            {
                logger.LogMessage(LogLevel.Warning, "Realm '{0}' failed but will be restarted", r.Name);
                r.RestartRequested = true;
            }

            SetEventInterval(r, 0);
            r.Deactivate();
            return TaskConstants.Completed;
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

        private async Task EnterInstance(Player player, Instance instance, string position = null, bool traveling = false)
        {
            Instance prevRealm;
            using (await player.Lock.WriterLockAsync())
            {
                prevRealm = player.Instance;
                if (!traveling)
                    player.SetAttribute("waygone", null);
            }

            if (prevRealm == instance)
                return;

            if (prevRealm != null)
            {
                logger.LogMessage(LogLevel.Verbose,
                    "{0} (#{1}) leaving '{2}'.",
                    player.Name,
                    player.ID,
                    prevRealm.Name);

                await prevRealm.RemovePlayer(player);
            }

            using (await player.Lock.WriterLockAsync())
                player.Instance = instance;

            if (instance != null)
            {
                logger.LogMessage(LogLevel.Verbose,
                    "{0} (#{1}) entering '{2}'.",
                    player.Name,
                    player.ID,
                    instance.Name);

                if (!instance.IsActive)
                    instance.Activate();

                instance.AddPlayer(player, position);
            }
        }

        public void TransferPlayer(Player player, string spec)
        {
            QueueEvent(() => HandleTransferPlayer(player, spec));
        }

        private async Task HandleTransferPlayer(Player player, string spec)
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

            Instance dest = GetInstance(instanceName);
            if (dest != null)
            {
                if (dest == player.Instance)
                {
                    await TransferErrorAsync(player, instanceName, "you're already there");
                }
                else if (dest.Realm.IsCondemned)
                {
                    await TransferErrorAsync(player, instanceName, "that realm has been condemned");
                }
                else if (dest.Realm.GetAccessLevel(player) <= RealmAccessLevel.Banned)
                {
                    await TransferErrorAsync(player, instanceName, "you aren't allowed to enter that realm");
                }
                else
                {
                    dest.Activate();

                    // TODO: let the destination know which realm is trying to send a player, so it can accept only from certain source realms
                    string check = dest.SendAndGet("$knock " + token);
                    switch (check)
                    {
                        case "ok":
                            await EnterInstance(player, dest, "=" + token, true);
                            break;

                        case "full":
                            await TransferErrorAsync(player, instanceName, "that realm is full");
                            break;

                        case "invalid":
                            await TransferErrorAsync(player, instanceName,
                                "that realm has no entrance called \"" + token + "\"");
                            break;

                        default:
                            await TransferErrorAsync(player, instanceName,
                                "it failed mysteriously (\"" + check + "\")");
                            break;
                    }
                }
            }
            else
            {
                await TransferErrorAsync(player, instanceName, "there is no such place");
            }
        }

        private static async Task TransferErrorAsync(Player player, string realmName, string message)
        {
            using (await player.Lock.ReaderLockAsync())
            {
                if (player.Connection != null)
                {
                    await player.Connection.WriteLineAsync(
                        "*** This realm tried to send you to \"{0}\", but {1}. ***",
                        realmName, message);
                    await player.Connection.FlushOutputAsync();
                }
            }
        }
        
        private async Task ShowWhoList(Connection conn, Player player)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{0,-25} {1,-6} {2,-6} {3}", "Player", "Conn", "Idle", "Realm");
            sb.AppendLine();

            int count = 0;
            foreach (var c in openConnections.Keys.OrderByDescending(c => c.ConnectedTime))
            {
                Player p;
                TimeSpan connectedTime, idleTime;
                using (await c.Lock.ReaderLockAsync())
                {
                    p = c.Player;
                    connectedTime = c.ConnectedTime;
                    idleTime = c.IdleTime;
                }

                if (p != null)
                {
                    count++;

                    Realm r;
                    using (await p.Lock.ReaderLockAsync())
                        r = p.Realm;

                    string realmName;
                    if (r == null)
                        realmName = "<none>";
                    else if (r.GetAccessLevel(player) < RealmAccessLevel.Visible)
                        realmName = "<private>";
                    else
                        realmName = r.Name;

                    sb.AppendFormat("{0,-25} {1,-6} {2,-6} {3}",
                        p.Name,
                        FormatTimeSpan(connectedTime),
                        FormatTimeSpan(idleTime),
                        realmName);
                    sb.AppendLine();
                }
            }

            sb.AppendFormat("{0} player{1} connected.", count, count == 1 ? "" : "s");
            sb.AppendLine();
            await conn.WriteAsync(sb.ToString());
            await conn.FlushOutputAsync();
        }

        public string[][] GetWhoList()
        {
            var query = from c in openConnections.Keys
                        let player = c.Player
                        where player != null
                        let connTime = c.ConnectedTime
                        orderby connTime descending
                        select new string[] { player.Name, FormatTimeSpan(connTime), FormatTimeSpan(c.IdleTime) };

            return query.ToArray();
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

        /// <summary>
        /// Combines a player ID with a command to form an input line for a realm.
        /// </summary>
        /// <param name="conn">The connection that issued the command.</param>
        /// <param name="line">The command. Any necessary sanitization or encoding
        /// must have already been applied.</param>
        /// <param name="silent"><b>true</b> to suppress output from this command.</param>
        /// <returns>The complete input line.</returns>
        private string MakeInputLine(Connection conn, string line, bool silent)
        {
#if COVERUP
            return line;
#else
            return string.Format("{0}{1}:{2}",
                silent ? "$silent " : "",
                conn.Player.ID,
                line);
#endif
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

        private async Task GreetClient(Connection conn)
        {
            string file = Path.Combine(
                Properties.Settings.Default.RealmDataPath,
                Properties.Settings.Default.GreetingFileName);

            if (File.Exists(file))
            {
                try
                {
                    await conn.WriteAsync(await FileAsync.ReadAllText(file));
                    return;
                }
                catch
                {
                    // use default greeting
                }
            }

            // default greeting
            await conn.WriteLineAsync("Welcome to the server!");
            await conn.WriteLineAsync("Type \"connect <name> <password>\" to connect.");
            await conn.WriteLineAsync("To create a new character, use the web site: " + Properties.Settings.Default.WebAddress);
        }

        private async Task GreetClientAsync(Connection conn)
        {
            var file = Path.Combine(
                Properties.Settings.Default.RealmDataPath,
                Properties.Settings.Default.GreetingFileName);

            if (File.Exists(file))
            {
                try
                {
                    // TODO: use SendTextFile?
                    string greeting;
                    using (var rdr = File.OpenText(file))
                    {
                        greeting = await rdr.ReadToEndAsync();
                    }
                    await conn.WriteAsync(greeting);
                    return;
                }
                catch (IOException)
                {
                    // fall through to default greeting
                }
            }

            await conn.WriteLineAsync("Welcome to the server!");
            await conn.WriteLineAsync("Type \"connect <name> <password>\" to connect.");
            await conn.WriteLineAsync("To create a new character, use the web site: " + Properties.Settings.Default.WebAddress);
        }

        private async Task SendTextFile(Connection conn, string playerName, string fileName)
        {
            string path = Path.Combine(
                Properties.Settings.Default.RealmDataPath,
                fileName);

            if (File.Exists(path))
            {
                try
                {
                    string text = await FileAsync.ReadAllText(path);
                    text = text.Replace("%NAME%", playerName);
                    await conn.WriteAsync(text);
                }
                catch
                {
                    // no motd for you!
                }
            }
        }
    }
}
