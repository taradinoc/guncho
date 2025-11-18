using Guncho.Connections;
using Guncho.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Nito.AsyncEx;
using XML = Guncho.XML;

namespace Guncho.WebHost.Services
{
    /// <summary>
    /// Core implementation of game server services for ASP.NET Core.
    /// Replaces the monolithic Server class from Guncho.Core.
    /// </summary>
    public class GunchoServerServices : IPlayerService, IRealmService, IInstanceService, IConnectionService, IInstanceSite
    {
        private readonly IServerConfiguration _config;
        private readonly ILogger _logger;
        private readonly ISignalRConnectionManager _connectionManager;
        private readonly IServiceProvider _services;

        private readonly ConcurrentDictionary<string, Player> _players = new();
        private readonly ConcurrentDictionary<int, Player> _playersById = new();
        private readonly ConcurrentDictionary<string, Realm> _realms = new();
        private readonly ConcurrentDictionary<string, IInstance> _instances = new();
        private readonly ConcurrentDictionary<Player, IInstance> _playerInstances = new();
        private readonly ConcurrentDictionary<Connection, Task> _openConnections = new();
        private readonly List<RealmFactory> _realmFactories = new();

        // Event queue for serializing game logic
        private readonly AsyncProducerConsumerQueue<Func<Task>> _eventQueue = new();
        private Task? _eventTask;
        private volatile bool _running;

        // Timed event system
        private readonly ConcurrentPriorityQueue<TimedEvent> _timedEvents = new();
        private readonly ConcurrentDictionary<IInstance, TimedEvent> _timedEventsByInstance = new();

        private const int EVENT_GRANULARITY_MS = 100; // Check timed events every 100ms

        public GunchoServerServices(
            IServerConfiguration config,
            ILogger logger,
            ISignalRConnectionManager connectionManager,
            IServiceProvider services)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        #region IPlayerService

        public Task<Player?> GetPlayerByNameAsync(string name)
        {
            _players.TryGetValue(name.ToLower(), out var player);
            return Task.FromResult(player);
        }

        public Task<Player?> GetPlayerByIdAsync(string id)
        {
            if (int.TryParse(id, out var playerId))
            {
                _playersById.TryGetValue(playerId, out var player);
                return Task.FromResult(player);
            }
            return Task.FromResult<Player?>(null);
        }

        public Task<bool> ValidateLogInAsync(Player player, string password)
        {
            ArgumentNullException.ThrowIfNull(player);

            if (string.IsNullOrEmpty(player.PasswordHash))
                return Task.FromResult(false);

            // Support both old SHA1+salt format and new BCrypt format
            if (!string.IsNullOrEmpty(player.PasswordSalt))
            {
                // Old format: SHA1 with salt
                var hash = HashPasswordOldTimey(player.PasswordSalt, password);
                return Task.FromResult(hash == player.PasswordHash);
            }
            else
            {
                // New format: BCrypt (hash includes salt)
                try
                {
                    return Task.FromResult(BCrypt.Net.BCrypt.Verify(password, player.PasswordHash));
                }
                catch
                {
                    return Task.FromResult(false);
                }
            }
        }

        private static string HashPasswordOldTimey(string salt, string password)
        {
            var bytes = new List<byte>();
            if (salt != null)
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(salt));
            if (password != null)
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(password));

            using var sha1 = System.Security.Cryptography.SHA1.Create();
            byte[] hash = sha1.ComputeHash(bytes.ToArray());
            return Convert.ToBase64String(hash);
        }

        public async Task SavePlayerAsync(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            _players[player.Name] = player;
            _playersById[player.ID] = player;

            // Save to XML file
            await SavePlayerIndexAsync();
        }

        public async Task DeletePlayerAsync(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            _players.TryRemove(player.Name, out _);
            _playersById.TryRemove(player.ID, out _);

            // Save to XML file
            await SavePlayerIndexAsync();
        }

        public IEnumerable<Player> GetAllPlayers()
        {
            return _players.Values;
        }

        private async Task LoadPlayersAsync()
        {
            var dataPath = _config.RealmDataPath;
            var playerIndexPath = Path.Combine(dataPath, "playerIndex.xml");

            if (!File.Exists(playerIndexPath))
            {
                _logger.LogMessage(LogLevel.Warning, $"Player index file not found at {playerIndexPath}");
                return;
            }

            try
            {
                var serializer = new XmlSerializer(typeof(XML.playerIndex));
                using var fs = new FileStream(playerIndexPath, FileMode.Open, FileAccess.Read);
                var index = (XML.playerIndex?)serializer.Deserialize(fs);

                if (index?.Item?.player != null)
                {
                    foreach (var entry in index.Item.player)
                    {
                        var player = new Player(entry.id, entry.name, entry.admin || entry.adminSpecified)
                        {
                            PasswordSalt = entry.pwdSalt ?? "",
                            PasswordHash = entry.pwdHash ?? ""
                        };

                        // Load attributes
                        if (entry.attribute != null)
                        {
                            foreach (var attr in entry.attribute)
                            {
                                player.SetAttribute(attr.name, attr.Value);
                            }
                        }

                        // Store with lowercase key for case-insensitive lookups
                        _players[player.Name.ToLower()] = player;
                        _playersById[player.ID] = player;
                    }
                }

                _logger.LogMessage(LogLevel.Verbose, $"Loaded {_players.Count} players");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
                throw;
            }
        }

        private async Task SavePlayerIndexAsync()
        {
            var dataPath = _config.RealmDataPath;
            var playerIndexPath = Path.Combine(dataPath, "playerIndex.xml");

            try
            {
                var index = new XML.playerIndex
                {
                    Item = new XML.playerIndexPlayers
                    {
                        player = _players.Values.Select(p => new XML.playerIndexPlayersPlayer
                        {
                            id = p.ID,
                            name = p.Name,
                            admin = p.IsAdmin,
                            adminSpecified = p.IsAdmin,
                            pwdSalt = p.PasswordSalt,
                            pwdHash = p.PasswordHash,
                            attribute = p.GetAllAttributes().Select(kvp => new XML.playerIndexPlayersPlayerAttribute
                            {
                                name = kvp.Key,
                                Value = kvp.Value
                            }).ToArray()
                        }).ToArray()
                    }
                };

                var serializer = new XmlSerializer(typeof(XML.playerIndex));
                using var fs = new FileStream(playerIndexPath, FileMode.Create, FileAccess.Write);
                serializer.Serialize(fs, index);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
                throw;
            }
        }

        #endregion

        #region IRealmService

        private async Task LoadRealmsAsync()
        {
            try
            {
                var dataPath = _config.RealmDataPath;
                var realmIndexPath = Path.Combine(dataPath, "realmIndex.xml");

                if (!File.Exists(realmIndexPath))
                {
                    _logger.LogMessage(LogLevel.Warning, $"Realm index not found: {realmIndexPath}");
                    return;
                }

                // Deserialize realm index
                XML.realmIndex index;
                var serializer = new XmlSerializer(typeof(XML.realmIndex));
                using (var fs = new FileStream(realmIndexPath, FileMode.Open, FileAccess.Read))
                {
                    index = (XML.realmIndex)serializer.Deserialize(fs)!;
                }

                if (index.realms == null || index.realms.Length == 0)
                {
                    _logger.LogMessage(LogLevel.Verbose, "No realms to load");
                    return;
                }

                // Load each realm
                foreach (var entry in index.realms)
                {
                    try
                    {
                        var owner = await GetPlayerByNameAsync(entry.owner);
                        if (owner == null)
                        {
                            _logger.LogMessage(LogLevel.Warning, $"Realm '{entry.name}' owner '{entry.owner}' not found, skipping");
                            continue;
                        }

                        // Find factory
                        var factory = _realmFactories.FirstOrDefault(f => f.Name == entry.factory) 
                                    ?? _realmFactories.FirstOrDefault();
                        
                        if (factory == null)
                        {
                            _logger.LogMessage(LogLevel.Warning, $"No factory available for realm '{entry.name}', skipping");
                            continue;
                        }

                        var sourceFile = Path.Combine(dataPath, entry.src);
                        if (!File.Exists(sourceFile))
                        {
                            _logger.LogMessage(LogLevel.Warning, $"Realm source file not found: {sourceFile}, skipping realm '{entry.name}'");
                            continue;
                        }

                        // For now, just create a placeholder realm without loading the story file
                        // Full compilation will happen when needed
                        var storyFile = Path.Combine(_config.CachePath, entry.name + ".ulx");
                        var realm = new Realm(factory, _config, entry.name, sourceFile, storyFile, owner);

                        // Set privacy level
                        realm.PrivacyLevel = entry.privacy switch
                        {
                            XML.privacyType.hidden => RealmPrivacyLevel.Hidden,
                            XML.privacyType.@private => RealmPrivacyLevel.Private,
                            XML.privacyType.@public => RealmPrivacyLevel.Public,
                            XML.privacyType.joinable => RealmPrivacyLevel.Joinable,
                            XML.privacyType.viewable => RealmPrivacyLevel.Viewable,
                            _ => RealmPrivacyLevel.Joinable
                        };

                        // Load ACL
                        if (entry.access != null)
                        {
                            var aclEntries = new List<RealmAccessListEntry>();
                            foreach (var accessEntry in entry.access)
                            {
                                var player = await GetPlayerByNameAsync(accessEntry.player);
                                if (player == null)
                                {
                                    _logger.LogMessage(LogLevel.Warning, $"ACL player '{accessEntry.player}' not found for realm '{entry.name}'");
                                    continue;
                                }

                                var level = accessEntry.level switch
                                {
                                    XML.levelType.banned => RealmAccessLevel.Banned,
                                    XML.levelType.editAccess => RealmAccessLevel.EditAccess,
                                    XML.levelType.editSettings => RealmAccessLevel.EditSettings,
                                    XML.levelType.editSource => RealmAccessLevel.EditSource,
                                    XML.levelType.hidden => RealmAccessLevel.Hidden,
                                    XML.levelType.invited => RealmAccessLevel.Invited,
                                    XML.levelType.safetyOff => RealmAccessLevel.SafetyOff,
                                    _ => RealmAccessLevel.Invited
                                };

                                aclEntries.Add(new RealmAccessListEntry(player, level));
                            }
                            realm.AccessList = aclEntries.ToArray();
                        }

                        _realms[entry.name] = realm;
                        _logger.LogMessage(LogLevel.Verbose, $"Loaded realm: {entry.name}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogMessage(LogLevel.Error, $"Error loading realm '{entry.name}': {ex.Message}");
                    }
                }

                _logger.LogMessage(LogLevel.Verbose, $"Loaded {_realms.Count} realms");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
                throw;
            }
        }

        #endregion

        #region IRealmService

        public Realm? GetRealm(string name)
        {
            _realms.TryGetValue(name, out var realm);
            return realm;
        }

        public Task<Realm?> GetRealmAsync(string name)
        {
            return Task.FromResult(GetRealm(name));
        }

        public async Task SaveRealmAsync(Realm realm)
        {
            ArgumentNullException.ThrowIfNull(realm);

            _realms[realm.Name] = realm;

            // TODO: Save realm to XML and persist game state
            await Task.CompletedTask;
        }

        public IEnumerable<Realm> GetAllRealms()
        {
            return _realms.Values;
        }

        public IEnumerable<RealmFactory> GetRealmFactories()
        {
            return _realmFactories;
        }

        public async Task<Realm?> CreateRealmAsync(Player owner, string name, RealmFactory factory)
        {
            var key = name.ToLower();

            if (!IsValidRealmName(name))
            {
                _logger.LogMessage(LogLevel.Warning, $"Invalid realm name: {name}");
                return null;
            }

            if (_realms.ContainsKey(key))
            {
                _logger.LogMessage(LogLevel.Warning, $"Realm already exists: {name}");
                return null;
            }

            // Enforce limit on number of realms per player
            if (!owner.IsAdmin)
            {
                int count = _realms.Values.Count(r => r.Owner == owner);
                int maxRealms = 5; // TODO: Make this configurable
                if (count >= maxRealms)
                {
                    _logger.LogMessage(LogLevel.Warning, $"Player {owner.Name} has reached realm limit ({maxRealms})");
                    return null;
                }
            }

            // Create source file
            var sourceFileName = NewSourceFileName(owner.Name, name, factory.SourceFileExtension);
            var sourcePath = Path.Combine(_config.RealmDataPath, sourceFileName);
            var initialSource = factory.GetInitialSourceText(owner.Name, name);
            await File.WriteAllTextAsync(sourcePath, initialSource);

            try
            {
                // Create realm object
                var storyFile = Path.Combine(_config.CachePath, name + ".ulx");
                var realm = new Realm(factory, _config, name, sourcePath, storyFile, owner);
                realm.PrivacyLevel = RealmPrivacyLevel.Joinable;

                _realms[key] = realm;
                await SaveRealmsAsync();

                _logger.LogMessage(LogLevel.Verbose, $"Created realm: {name}");
                return realm;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
                // Clean up source file if realm creation failed
                try { File.Delete(sourcePath); } catch { }
                return null;
            }
        }

        public async Task<RealmEditingOutcome> UpdateRealmSourceAsync(Realm realm)
        {
            ArgumentNullException.ThrowIfNull(realm);

            _logger.LogMessage(LogLevel.Verbose, $"Updating source for realm '{realm.Name}'.");

            // Compile to a temporary ULX first (using DB-backed assets when available)
            var tempUlx = Path.Combine(_config.CachePath, $"{realm.Name}.preview.ulx");
            try
            {
                RealmEditingOutcome outcome;
                // Try to compile from database assets
                using (var scope = _services.CreateScope())
                {
                    var realmRepo = scope.ServiceProvider.GetService<Guncho.Repositories.RealmRepository>();
                    var assetRepo = scope.ServiceProvider.GetService<Guncho.Repositories.RealmAssetRepository>();

                    var meta = realmRepo != null ? await realmRepo.GetByNameAsync(realm.Name) : null;
                    if (meta != null && assetRepo != null)
                    {
                        var assets = await assetRepo.GetAllContentForRealmAsync(meta.Id);
                        if (assets.Count > 0)
                        {
                            // Determine main file
                            var mainFile = meta.MainFile;
                            if (string.IsNullOrWhiteSpace(mainFile))
                            {
                                mainFile = realm.Factory.DefaultMainFileName;
                                if (!assets.ContainsKey(mainFile))
                                {
                                    // Heuristics: pick first .ni for I7 or first .inf for I6
                                    var preferredExt = realm.Factory.SourceFileExtension;
                                    var candidate = assets.Keys.FirstOrDefault(k => k.EndsWith(preferredExt, StringComparison.OrdinalIgnoreCase));
                                    if (!string.IsNullOrEmpty(candidate)) mainFile = candidate;
                                }
                            }

                            outcome = await realm.Factory.CompileRealmAsync(realm.Name, assets, mainFile!, tempUlx);
                        }
                        else
                        {
                            // Fallback to legacy single-file path via assets API
                            var fileName = Path.GetFileName(realm.SourceFile);
                            var fileBytes = await File.ReadAllBytesAsync(realm.SourceFile);
                            var dict = new Dictionary<string, byte[]> { [fileName] = fileBytes };
                            outcome = await realm.Factory.CompileRealmAsync(realm.Name, dict, fileName, tempUlx);
                        }
                    }
                    else
                    {
                        // Fallback to legacy single-file path via assets API
                        var fileName = Path.GetFileName(realm.SourceFile);
                        var fileBytes = await File.ReadAllBytesAsync(realm.SourceFile);
                        var dict = new Dictionary<string, byte[]> { [fileName] = fileBytes };
                        outcome = await realm.Factory.CompileRealmAsync(realm.Name, dict, fileName, tempUlx);
                    }
                }
                if (outcome != RealmEditingOutcome.Success)
                {
                    _logger.LogMessage(LogLevel.Warning, $"Compile failed for realm '{realm.Name}' with outcome {outcome}.");
                    try { if (File.Exists(tempUlx)) File.Delete(tempUlx); } catch { }
                    return outcome;
                }

                // Smoke-test the compiled ULX by creating a temporary instance against it
                try
                {
                    using var fs = new FileStream(tempUlx, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var testInstance = new FyreVMInstance(this, _config, realm, fs, $"{realm.Name}:preflight", _logger);
                    await testInstance.ActivateAsync();
                    await testInstance.PolitelyDisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogMessage(LogLevel.Error, $"VM preflight failed for realm '{realm.Name}': {ex.Message}");
                    try { if (File.Exists(tempUlx)) File.Delete(tempUlx); } catch { }
                    return RealmEditingOutcome.VMError;
                }

                // Preflight succeeded: now capture player positions and stop original instances
                var originalInstances = _instances.Values.Where(inst => inst.Realm == realm).ToArray();
                var savedPositions = new ConcurrentDictionary<string, ConcurrentDictionary<Player, string>>();

                foreach (var inst in originalInstances)
                {
                    var dict = new ConcurrentDictionary<Player, string>();
                    savedPositions.TryAdd(inst.Name, dict);
                    await inst.ExportPlayerPositionsAsync(dict);
                    await SetEventIntervalAsync(inst, 0);
                    await inst.PolitelyDisposeAsync();
                    _instances.TryRemove(inst.Name, out _);
                }

                // Swap ULX into place (robust overwrite)
                try
                {
                    // Prefer overwrite copy to avoid IOException if target exists
                    File.Copy(tempUlx, realm.StoryFile, true);
                    File.Delete(tempUlx);
                }
                catch (Exception ex)
                {
                    _logger.LogMessage(LogLevel.Warning, $"Primary ULX overwrite failed for realm '{realm.Name}': {ex.Message}. Attempting fallback move.");
                    try
                    {
                        if (File.Exists(realm.StoryFile))
                        {
                            try { File.Delete(realm.StoryFile); } catch { }
                        }
                        File.Move(tempUlx, realm.StoryFile);
                    }
                    catch (Exception ex2)
                    {
                        _logger.LogMessage(LogLevel.Error, $"ULX replacement failed for realm '{realm.Name}': {ex2.Message}");
                        try { if (File.Exists(tempUlx)) File.Delete(tempUlx); } catch { }
                        return RealmEditingOutcome.VMError;
                    }
                }

                _logger.LogMessage(LogLevel.Verbose, $"Reloading realm '{realm.Name}'.");

                // Recreate instances and re-enter players
                var connectionsByPlayer = _openConnections.Keys.ToLookup(c => c.Player);

                foreach (var instEntry in savedPositions)
                {
                    var newInst = realm.Factory.LoadInstance(this, realm, instEntry.Key, _logger);
                    _instances[instEntry.Key] = newInst;
                    await newInst.ActivateAsync();

                    foreach (var kvp in instEntry.Value)
                    {
                        var p = kvp.Key;
                        var pos = kvp.Value;

                        await Task.WhenAll(connectionsByPlayer[p].Select(async conn =>
                        {
                            await conn.WriteLineAsync("[The realm shimmers for a moment...]");
                        }));

                        await EnterInstanceAsync(p, newInst, pos);
                    }
                }

                return RealmEditingOutcome.Success;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
                try { if (File.Exists(tempUlx)) File.Delete(tempUlx); } catch { }

                // On failure, move players to the start realm if possible
                try
                {
                    var startRealm = _realms.Values.FirstOrDefault(r => r.Name.Equals(_config.StartRealmName, StringComparison.OrdinalIgnoreCase));
                    if (startRealm != null)
                    {
                        var startInst = await GetDefaultInstanceAsync(startRealm);
                        foreach (var inst in _instances.Values.Where(i => i.Realm == realm).ToArray())
                        {
                            var dict = new ConcurrentDictionary<Player, string>();
                            await inst.ExportPlayerPositionsAsync(dict);
                            await SetEventIntervalAsync(inst, 0);
                            await inst.PolitelyDisposeAsync();
                            _instances.TryRemove(inst.Name, out _);

                            foreach (var kvp in dict)
                            {
                                var p = kvp.Key;
                                await WithPlayerConnectionsAsync(p, async conn => await conn.WriteLineAsync("[The realm has failed.]") );
                                await EnterInstanceAsync(p, startInst, kvp.Value);
                            }
                        }
                    }
                }
                catch { }

                return RealmEditingOutcome.VMError;
            }
        }

        private async Task SaveRealmsAsync()
        {
            try
            {
                var dataPath = _config.RealmDataPath;
                var realmIndexPath = Path.Combine(dataPath, "realmIndex.xml");

                var index = new XML.realmIndex();
                var entries = new List<XML.realmIndexRealm>();

                foreach (var realm in _realms.Values)
                {
                    var item = new XML.realmIndexRealm
                    {
                        name = realm.Name,
                        src = Path.GetFileName(realm.SourceFile),
                        owner = realm.Owner.Name,
                        factory = realm.Factory.Name,
                        privacy = realm.PrivacyLevel switch
                        {
                            RealmPrivacyLevel.Hidden => XML.privacyType.hidden,
                            RealmPrivacyLevel.Private => XML.privacyType.@private,
                            RealmPrivacyLevel.Public => XML.privacyType.@public,
                            RealmPrivacyLevel.Joinable => XML.privacyType.joinable,
                            RealmPrivacyLevel.Viewable => XML.privacyType.viewable,
                            _ => XML.privacyType.joinable
                        }
                    };

                    // Build ACL
                    if (realm.AccessList.Length > 0)
                    {
                        var acl = new List<XML.realmIndexRealmAccess>();
                        foreach (var entry in realm.AccessList)
                        {
                            var xent = new XML.realmIndexRealmAccess
                            {
                                player = entry.Player.Name,
                                level = entry.Level switch
                                {
                                    RealmAccessLevel.Banned => XML.levelType.banned,
                                    RealmAccessLevel.EditAccess => XML.levelType.editAccess,
                                    RealmAccessLevel.EditSettings => XML.levelType.editSettings,
                                    RealmAccessLevel.EditSource => XML.levelType.editSource,
                                    RealmAccessLevel.Hidden => XML.levelType.hidden,
                                    RealmAccessLevel.Invited => XML.levelType.invited,
                                    RealmAccessLevel.SafetyOff => XML.levelType.safetyOff,
                                    _ => XML.levelType.invited
                                }
                            };
                            acl.Add(xent);
                        }
                        item.access = acl.ToArray();
                    }

                    entries.Add(item);
                }

                index.realms = entries.ToArray();

                // Serialize to file
                var serializer = new XmlSerializer(typeof(XML.realmIndex));
                using (var fs = new FileStream(realmIndexPath, FileMode.Create, FileAccess.Write))
                {
                    serializer.Serialize(fs, index);
                }

                _logger.LogMessage(LogLevel.Verbose, $"Saved {_realms.Count} realms to realmIndex.xml");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
                throw;
            }
        }

        private static bool IsValidRealmName(string name)
        {
            if (name != name.Trim())
                return false;

            if (name.Length == 0)
                return false;

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;

            return true;
        }

        private static string NewSourceFileName(string ownerName, string realmName, string ext)
        {
            if (ext.Length > 0 && !ext.StartsWith(".", StringComparison.Ordinal))
                ext = "." + ext;

            var sb = new System.Text.StringBuilder();
            sb.Append(ownerName.ToLower());
            sb.Append('_');
            sb.Append(realmName.ToLower());

            // Replace invalid filename characters with underscores
            var invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < sb.Length; i++)
                if (Array.IndexOf(invalid, sb[i]) >= 0)
                    sb[i] = '_';

            sb.Append(ext);
            return sb.ToString();
        }

        #endregion

        #region IInstanceService

        public IInstance? GetInstance(string name)
        {
            ArgumentNullException.ThrowIfNull(name);

            // Try direct lookup first (for instance names like "RealmName:default")
            if (_instances.TryGetValue(name, out var instance))
                return instance;

            // Try case-insensitive lookup by realm name
            if (_instances.TryGetValue(name.ToLower(), out instance))
                return instance;

            // Try to find realm and get its default instance
            var realm = GetRealm(name);
            if (realm != null)
            {
                var defaultName = realm.Name.ToLower();
                _instances.TryGetValue(defaultName, out instance);
            }

            return instance;
        }

        public async Task<IInstance> GetDefaultInstanceAsync(Realm realm)
        {
            ArgumentNullException.ThrowIfNull(realm);

            // Use lowercase realm name as instance key (legacy compatibility)
            var instanceName = realm.Name.ToLower();
            if (_instances.TryGetValue(instanceName, out var existingInstance))
                return existingInstance;

            // Create new instance through the factory (pass original name to factory)
            var instance = realm.Factory.LoadInstance(this, realm, realm.Name, _logger);
            _instances[instanceName] = instance;

            await Task.CompletedTask;
            return instance;
        }

        public async Task EnterInstanceAsync(Player player, IInstance instance, string? savedPosition = null)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(instance);

            // Remove from previous instance if needed
            if (_playerInstances.TryGetValue(player, out var prevInstance) && prevInstance != instance)
            {
                _logger.LogMessage(LogLevel.Verbose, "{0} (#{1}) leaving '{2}'", player.Name, player.ID, prevInstance.Name);
                await prevInstance.RemovePlayerAsync(player);
            }

            _playerInstances[player] = instance;

            _logger.LogMessage(LogLevel.Verbose, "{0} (#{1}) entering '{2}'", player.Name, player.ID, instance.Name);

            // Activate instance if not already active
            if (!instance.IsActive)
            {
                await instance.ActivateAsync();
            }

            // Add player to instance
            await instance.AddPlayerAsync(player, savedPosition ?? string.Empty);
        }

        public IDictionary<Player, IInstance> GetPlayerInstances()
        {
            return _playerInstances;
        }

        #endregion

        #region IConnectionService

        public IEnumerable<Connection> GetOpenConnections()
        {
            return _openConnections.Keys;
        }

        public async Task<bool> WithPlayerConnectionsAsync(Player player, Func<Connection, Task> action)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(action);

            var connections = _openConnections.Keys.Where(c => c.Player == player).ToList();
            if (connections.Count == 0)
                return false;

            await Task.WhenAll(connections.Select(action));
            return true;
        }

        public async Task SendTextFileAsync(Connection connection, string filePath)
        {
            ArgumentNullException.ThrowIfNull(connection);
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var fullPath = Path.Combine(_config.RealmDataPath, filePath);

            if (!File.Exists(fullPath))
            {
                _logger.LogMessage(LogLevel.Warning, $"Text file not found: {fullPath}");
                return;
            }

            var lines = await File.ReadAllLinesAsync(fullPath);
            foreach (var line in lines)
            {
                await connection.WriteLineAsync(line);
            }
        }

        #endregion

        #region Initialization

        public async Task InitializeAsync()
        {
            _logger.LogMessage(LogLevel.Verbose, "Initializing Guncho server services...");

            // Create required directories
            Directory.CreateDirectory(_config.CachePath);
            Directory.CreateDirectory(_config.IndexPath);
            Directory.CreateDirectory(_config.RealmDataPath);
            Directory.CreateDirectory(_config.LogPath);

            // Register realm factories
            RegisterRealmFactories();

            // Load players first (realms need player references)
            await LoadPlayersAsync();

            // Load realms
            await LoadRealmsAsync();

            // Subscribe to connection events
            _connectionManager.ConnectionAccepted += OnConnectionAccepted;
            _connectionManager.ConnectionClosed += OnConnectionClosed;

            // Start event processing loop
            _running = true;
            _eventTask = Task.Run(ProcessEventsAsync);

            _logger.LogMessage(LogLevel.Verbose, "Guncho server services initialized");
        }

        private void RegisterRealmFactories()
        {
            try
            {
                var installationsPath = _config.NiInstallationsPath;
                if (!Directory.Exists(installationsPath))
                {
                    _logger.LogMessage(LogLevel.Warning, $"Inform installations path not found: {installationsPath}");
                    return;
                }

                var factories = InformRealmFactory.ConstructAll(
                    config: _config,
                    logger: _logger,
                    installationsPath: installationsPath,
                    indexOutputDir: _config.IndexPath);

                _realmFactories.AddRange(factories);
                _logger.LogMessage(LogLevel.Verbose, $"Registered {factories.Length} Inform 7 realm factories");

                // Register Inform 6 realm factory
                var inform6CompilerPath = _config.Inform6CompilerPath;
                var inform6LibraryPath = _config.Inform6LibraryPath;
                var inform6Factories = Guncho.Inform6RealmFactory.ConstructAll(
                    config: _config,
                    logger: _logger,
                    compilerPath: inform6CompilerPath,
                    libraryPath: inform6LibraryPath,
                    indexOutputDir: _config.IndexPath);
                _realmFactories.AddRange(inform6Factories);
                _logger.LogMessage(LogLevel.Verbose, $"Registered {inform6Factories.Length} Inform 6 realm factories");
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, $"Failed to register realm factories: {ex.Message}");
            }
        }

        #endregion

        #region IInstanceSite

        public void NotifyInstanceFinished(IInstance instance, Player[] abandoned, bool wasTerminated)
        {
            // TODO: Handle instance cleanup, save player positions, remove from tracking
            _instances.TryRemove(instance.Name, out _);
        }

        public async Task SetEventIntervalAsync(IInstance instance, int seconds)
        {
            ArgumentNullException.ThrowIfNull(instance);
            if (seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));

            TimedEvent? prev;
            if (_timedEventsByInstance.TryGetValue(instance, out prev))
            {
                if (prev.Interval != seconds)
                    prev.Deleted = true;
                else
                    return;
            }

            if (seconds == 0)
            {
                _timedEventsByInstance.TryRemove(instance, out _);
            }
            else
            {
                var ev = new TimedEvent(instance, seconds);
                _timedEventsByInstance[instance] = ev;
                await _timedEvents.EnqueueAsync(ev, ev.Time.ToFileTime());
            }
        }

        public async Task<bool> FlushPlayerAsync(Player p)
        {
            var sent = false;
            await WithPlayerConnectionsAsync(p, async conn =>
            {
                await conn.FlushOutputAsync();
                sent = true;
            });
            return sent;
        }

        public async Task<bool> SendLineToPlayerAsync(Player p)
        {
            var sent = false;
            await WithPlayerConnectionsAsync(p, async conn =>
            {
                await conn.WriteLineAsync(string.Empty);
                sent = true;
            });
            return sent;
        }

        public async Task<bool> SendToPlayerAsync(Player p, char c)
        {
            var sent = false;
            await WithPlayerConnectionsAsync(p, async conn =>
            {
                await conn.WriteAsync(c.ToString());
                sent = true;
            });
            return sent;
        }

        public async Task<bool> SendToPlayerAsync(Player p, string s)
        {
            var sent = false;
            await WithPlayerConnectionsAsync(p, async conn =>
            {
                await conn.WriteAsync(s);
                sent = true;
            });
            return sent;
        }

        public void TransferPlayer(Player p, string spec)
        {
            // TODO: Implement player transfer between realms/instances
            _logger.LogMessage(LogLevel.Verbose, $"Player transfer requested: {p.Name} -> {spec}");
        }

        public TimeSpan? GetPlayerIdleTime(Player queriedPlayer)
        {
            // TODO: Track player activity timestamps
            return null;
        }

        #endregion

        #region Connection Handling

        /// <summary>
        /// Register a new connection (TCP or other non-SignalR transports) with the server.
        /// </summary>
        public void RegisterConnection(Connection conn, string? authenticatedUser = null)
        {
            conn.FilterBlankLines = _config.FilterBlankLines;
            var connTask = HandleConnectionAsync(conn, authenticatedUser);
            _openConnections.TryAdd(conn, connTask);
        }

        /// <summary>
        /// Unregister a connection when it closes.
        /// </summary>
        public void UnregisterConnection(Connection conn)
        {
            _openConnections.TryRemove(conn, out _);
        }

        private void OnConnectionAccepted(object? sender, ConnectionAcceptedEventArgs e)
        {
            _logger.LogMessage(LogLevel.Verbose, "SignalR: Accepting connection with ID {0}.", e.Connection.ConnectionId);
            e.Connection.FilterBlankLines = _config.FilterBlankLines;
            var connTask = HandleConnectionAsync(e.Connection, e.AuthenticatedUserName);
            _openConnections.TryAdd(e.Connection, connTask);
        }

        private void OnConnectionClosed(object? sender, ConnectionClosedEventArgs e)
        {
            _logger.LogMessage(LogLevel.Verbose, "SignalR: Lost connection with ID {0}.", e.Connection.ConnectionId);
            _openConnections.TryRemove(e.Connection, out _);
        }

        private async Task HandleConnectionAsync(Connection conn, string? authenticatedUser = null)
        {
            if (authenticatedUser != null)
            {
                // log them in immediately
                if (authenticatedUser.ToLower() == "guest")
                {
                    await LogInAsGuestAsync(conn);
                }
                else
                {
                    var player = await GetPlayerByNameAsync(authenticatedUser.ToLower());
                    if (player != null)
                    {
                        await LogInAsPlayerAsync(conn, player);
                    }
                    else
                    {
                        _logger.LogMessage(LogLevel.Error, "Can't auto-login authenticatedUser because they don't exist: {0}", authenticatedUser);
                        await GreetClientAsync(conn);
                    }
                }
            }
            else
            {
                await GreetClientAsync(conn);
            }

            var cancellationToken = CancellationToken.None; // TODO: Use proper cancellation
            
            string? line;
            while ((line = await conn.ReadLineAsync(cancellationToken)) != null)
            {
                line = TrimAndHandleBackspace(line);

                IInstance? instance = null;
                if (conn.Player != null)
                {
                    _playerInstances.TryGetValue(conn.Player, out instance);
                }

                if (instance == null)
                {
                    // only handle out-of-realm commands (connect, create, quit, who)
                    if (!UnpackHandleSystemCommandResult(await HandleSystemCommandAsync(conn, line), out _))
                    {
                        await conn.WriteLineAsync("Unknown command.");
                        await conn.WriteLineAsync();
                        await GreetClientAsync(conn);
                    }
                }
                else if (UnpackHandleSystemCommandResult(await HandleSystemCommandAsync(conn, line), out line))
                {
                    // go on to the next line
                    continue;
                }
                else if (conn.Player != null)
                {
                    string? dabString;

                    using (await conn.Player.Lock.WriterLockAsync())
                    {
                        dabString = conn.Player.Disambiguating;
                        conn.Player.Disambiguating = null;
                    }

                    if (dabString != null)
                    {
                        // Repeat the previous command, but hide its output (the disambiguation question)
                        instance.QueueInput(MakeInputLine(conn, dabString, true));
                        // Provide the answer
                        if (line != null) instance.QueueInput(line);
                    }
                    else
                    {
                        // Pass the line into the realm
                        if (line != null)
                        {
                            // Rewrite chat commands (say, emote, etc.) before passing to realm
                            line = RewriteChatCommandsIfNeeded(line);
                            instance.QueueInput(MakeInputLine(conn, line, false));
                        }
                    }
                }
            }

            _logger.LogMessage(LogLevel.Notice, "HandleConnectionAsync: Connection lost");
            
            var disconnectedPlayer = conn.Player;
            if (disconnectedPlayer != null)
            {
                if (conn is TcpConnection tcpConn && tcpConn.OtherSide != null)
                {
                    // Mirror connect log style at NOTICE level for disconnects
                    _logger.LogMessage(LogLevel.Notice, "TCP: Connection closed from {0}.", tcpConn.OtherSide);
                }
                // If the player was in an instance, remove them so a subsequent
                // reconnect can join cleanly and the VM sees a $part event.
                if (_playerInstances.TryGetValue(disconnectedPlayer, out var inst))
                {
                    try
                    {
                        await inst.RemovePlayerAsync(disconnectedPlayer);
                        _playerInstances.TryRemove(disconnectedPlayer, out _);
                        _logger.LogMessage(LogLevel.Verbose, "Player {0} removed from instance {1} after connection loss.", disconnectedPlayer.Name, inst.Realm.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogMessage(LogLevel.Error, "Failed to remove player {0} from instance on disconnect: {1}", disconnectedPlayer.Name, ex.Message);
                    }
                }

                ReleaseGuest(disconnectedPlayer);
            }
        }

        private string TrimAndHandleBackspace(string line)
        {
            // TODO: Implement backspace handling
            return line.Trim();
        }

        private string MakeInputLine(Connection conn, string line, bool hidden)
        {
            if (conn.Player == null)
                return line;
                
            return string.Format("{0}{1}:{2}",
                hidden ? "$silent " : "",
                conn.Player.ID,
                line);
        }

        private void ReleaseGuest(Player player)
        {
            if (player == null || !player.IsGuest)
                return;

            var key = player.Name.ToLowerInvariant();
            if (_players.TryRemove(key, out _))
            {
                _logger.LogMessage(LogLevel.Verbose, "Released guest slot {0}.", player.Name);
            }

            _playersById.TryRemove(player.ID, out _);
        }

        private struct HandleSystemCommandResult
        {
            public bool Handled;
            public string? Line;
        }

        private static bool UnpackHandleSystemCommandResult(HandleSystemCommandResult result, out string? line)
        {
            line = result.Line;
            return result.Handled;
        }

        private async Task<HandleSystemCommandResult> HandleSystemCommandAsync(Connection conn, string line)
        {
            string trimmed = line.Trim();
            string command = GetToken(ref trimmed, ' ').ToLower();

            var result = new HandleSystemCommandResult
            {
                Handled = true,
                Line = line,
            };

            // ignore blank lines
            if (command.Length == 0)
                return result;

            var player = conn.Player;

            // check commands that can be used any time
            switch (command)
            {
                case "who":
                    await ShowWhoListAsync(conn, player);
                    return result;

                case "quit":
                    // If the player is in an instance, explicitly remove them so the VM sees $part now.
                    if (player != null && _playerInstances.TryGetValue(player, out var quitInstance))
                    {
                        try
                        {
                            await quitInstance.RemovePlayerAsync(player);
                            _playerInstances.TryRemove(player, out _);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogMessage(LogLevel.Error, "Quit: failed RemovePlayerAsync for {0}: {1}", player.Name, ex.Message);
                        }
                    }

                    await conn.WriteLineAsync("Goodbye.");
                    await conn.FlushOutputAsync();
                    await conn.TerminateAsync();
                    
                    if (conn is TcpConnection tcpConn && tcpConn.OtherSide != null)
                    {
                        _logger.LogMessage(LogLevel.Notice, "TCP: Connection closed from {0} on quit command (player {1}).", tcpConn.OtherSide, player?.Name ?? "<none>");
                    }
                    else
                    {
                        _logger.LogMessage(LogLevel.Notice, "Connection terminated on quit command (player {0}).", player?.Name ?? "<none>");
                    }

                    if (player != null)
                    {
                        ReleaseGuest(player);
                    }
                    return result;
            }

            IInstance? instance;
            if (player == null || !_playerInstances.TryGetValue(player, out instance))
            {
                // check out-of-realm commands
                switch (command)
                {
                    case "connect":
                    case "connec":
                    case "conne":
                    case "conn":
                    case "con":
                    case "co":
                        string name = GetToken(ref trimmed, ' ');
                        if (name.Length == 0)
                        {
                            await conn.WriteLineAsync("Usage: connect <name> <password>");
                        }
                        else if (name.ToLower() == "guest")
                        {
                            await LogInAsGuestAsync(conn);
                        }
                        else
                        {
                            string password = GetToken(ref trimmed, ' ');
                            var loginPlayer = await ValidateLogInAsync(name, password);

                            if (loginPlayer == null || loginPlayer.IsGuest)
                            {
                                await conn.WriteLineAsync("Incorrect login.");
                            }
                            else
                            {
                                await LogInAsPlayerAsync(conn, loginPlayer);
                            }
                        }
                        return result;
                }
            }
            else
            {
                // check in-realm commands
                switch (command)
                {
                    case "@shutdown":
                        await CmdShutdownAsync(conn, player, trimmed);
                        return result;

                    case "@wall":
                        await CmdWallAsync(conn, player, trimmed);
                        return result;

                    case "@teleport":
                    case "@tel":
                        await CmdTeleportAsync(conn, player, trimmed);
                        return result;

                    case "page":
                    case "p":
                        await CmdPageAsync(conn, player, trimmed);
                        return result;

                    case "again":
                    case "g":
                        var lastCmd = player.LastCommand;
                        if (lastCmd == null)
                        {
                            await conn.WriteLineAsync("No previous command to repeat.");
                            return result;
                        }

                        result.Handled = false;
                        result.Line = player.LastCommand;
                        return result;
                }

                // save command for 'again'
                using (await player.Lock.WriterLockAsync())
                    player.LastCommand = line;
            }

            result.Handled = false;
            return result;
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

        private async Task<Player?> ValidateLogInAsync(string name, string password)
        {
            var player = await GetPlayerByNameAsync(name.ToLower());
            if (player != null && await ValidateLogInAsync(player, password))
            {
                return player;
            }
            return null;
        }

        private async Task GreetClientAsync(Connection conn)
        {
            await SendTextFileAsync(conn, null, _config.ConnectTextPath);
        }

        private async Task SendTextFileAsync(Connection conn, string? playerName, string path)
        {
            if (File.Exists(path))
            {
                var text = await File.ReadAllTextAsync(path);
                if (playerName != null)
                {
                    text = text.Replace("$NAME", playerName);
                    text = text.Replace("%NAME%", playerName);
                }
                await conn.WriteLineAsync(text);
                await conn.FlushOutputAsync();
            }
        }

        private async Task ShowWhoListAsync(Connection conn, Player? player)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendFormat("{0,-25} {1,-6} {2,-6} {3}", "Player", "Conn", "Idle", "Realm");
            sb.AppendLine();

            int count = 0;
            foreach (var c in _openConnections.Keys.OrderByDescending(c => c.ConnectedTime))
            {
                Player? p = c.Player;
                
                if (p != null)
                {
                    count++;

                    string realmName;
                    if (_playerInstances.TryGetValue(p, out var instance))
                    {
                        var r = instance.Realm;
                        
                        if (r == null)
                            realmName = "<none>";
                        else if (player != null && r.GetAccessLevel(player) < RealmAccessLevel.Visible)
                            realmName = "<private>";
                        else
                            realmName = r.Name;
                    }
                    else
                    {
                        realmName = "<lobby>";
                    }

                    sb.AppendFormat("{0,-25} {1,-6} {2,-6} {3}",
                        p.Name,
                        FormatTimeSpan(c.ConnectedTime),
                        FormatTimeSpan(c.IdleTime),
                        realmName);
                    sb.AppendLine();
                }
            }

            sb.AppendFormat("{0} player{1} connected.", count, count == 1 ? "" : "s");
            sb.AppendLine();
            await conn.WriteAsync(sb.ToString());
            await conn.FlushOutputAsync();
        }

        private async Task LogInAsPlayerAsync(Connection conn, Player player)
        {
            _logger.LogMessage(LogLevel.Spam, "Setting conn.Player");

            var oldConns = _openConnections.Keys.Where(c => c.Player == player).ToArray();

            using (await conn.Lock.WriterLockAsync())
                conn.Player = player;

            if (oldConns.Length > 0)
            {
                await Task.WhenAll(oldConns.Select(async c =>
                {
                    _logger.LogMessage(LogLevel.Spam, "notifyOldConn: Starting");
                    await c.WriteLineAsync("*** Connection superseded ***");
                    await c.TerminateAsync();
                    _logger.LogMessage(LogLevel.Spam, "notifyOldConn: Done");
                }));

                _logger.LogMessage(LogLevel.Spam, "notifyNewConn: Starting");
                await conn.WriteLineAsync("*** Connection resumed ***");
                await conn.FlushOutputAsync();
                _logger.LogMessage(LogLevel.Spam, "notifyNewConn: Done");
            }

            _logger.LogMessage(LogLevel.Spam, "Sending MOTD");
            await SendTextFileAsync(conn, player.Name, _config.MotdPath);
            
            _logger.LogMessage(LogLevel.Spam, "Entering instance");
            var startRealm = _realms.Values.FirstOrDefault(r => r.Name.Equals(_config.StartRealmName, StringComparison.OrdinalIgnoreCase));
            if (startRealm != null)
            {
                _logger.LogMessage(LogLevel.Verbose, "Auto-entering start realm: {0}", startRealm.Name);
                var defaultInstance = await GetDefaultInstanceAsync(startRealm);
                await EnterInstanceAsync(player, defaultInstance);
            }
            else
            {
                _logger.LogMessage(LogLevel.Warning, "Start realm not found: {0}", _config.StartRealmName);
                await conn.WriteLineAsync("Welcome back, " + player.Name + "!");
                await conn.WriteLineAsync("(Start realm not available)");
            }
            
            // Flush all buffered output (MOTD and realm entry messages)
            await conn.FlushOutputAsync();
            
            _logger.LogMessage(LogLevel.Spam, "Login complete");
        }

        private async Task LogInAsGuestAsync(Connection conn)
        {
            Player guest;

            int guestNum = 0;
            string guestName, key;
            do
            {
                guestNum++;
                guestName = "Guest" + guestNum.ToString();
                key = guestName.ToLower();
            } while (!_players.TryAdd(key, null!));

            guest = new Player(-guestNum, guestName, false, true);
            _players[key] = guest;
            _playersById[-guestNum] = guest;

            using (await conn.Lock.WriterLockAsync())
                conn.Player = guest;

            await SendTextFileAsync(conn, guest.Name, _config.GuestMotdPath);
            
            var startRealm = _realms.Values.FirstOrDefault(r => r.Name.Equals(_config.StartRealmName, StringComparison.OrdinalIgnoreCase));
            if (startRealm != null)
            {
                _logger.LogMessage(LogLevel.Verbose, "Auto-entering start realm for guest: {0}", startRealm.Name);
                var defaultInstance = await GetDefaultInstanceAsync(startRealm);
                await EnterInstanceAsync(guest, defaultInstance);
            }
            else
            {
                _logger.LogMessage(LogLevel.Warning, "Start realm not found: {0}", _config.StartRealmName);
                await conn.WriteLineAsync("Welcome, " + guest.Name + "!");
                await conn.WriteLineAsync("(Start realm not available)");
            }

            await conn.FlushOutputAsync();
        }

        private async Task CmdTeleportAsync(Connection conn, Player player, string args)
        {
            var dest = GetInstance(args.Trim());

            if (dest == null)
            {
                await conn.WriteLineAsync("No such realm.");
                return;
            }

            IInstance? inst;

            if (_playerInstances.TryGetValue(player, out inst) && inst == dest)
            {
                await conn.WriteLineAsync("You're already in that realm.");
                return;
            }

            var realm = dest.Realm;

            if (realm.GetAccessLevel(player) < RealmAccessLevel.Invited &&
                args.Trim().ToLower() != _config.StartRealmName.ToLower())
            {
                await conn.WriteLineAsync("Permission denied.");
                return;
            }

            if (realm.IsCondemned)
            {
                await conn.WriteLineAsync("That realm has been condemned.");
                return;
            }

            await dest.ActivateAsync();

            string check = await dest.SendAndGetAsync("$knock default");

            switch (check)
            {
                case "ok":
                    await EnterInstanceAsync(player, dest);
                    break;

                case "full":
                    await conn.WriteLineAsync("That realm is full.");
                    break;

                default:
                    await conn.WriteLineAsync("Teleporting failed mysteriously.");
                    break;
            }
        }

        private async Task CmdShutdownAsync(Connection conn, Player player, string args)
        {
            if (player.IsAdmin)
            {
                string msg = args.Trim();
                if (msg.Length == 0)
                    msg = "no reason specified";

                await ShutdownAsync(msg);
            }
            else
            {
                await conn.WriteLineAsync("Permission denied.");
            }
        }

        private async Task CmdWallAsync(Connection conn, Player player, string args)
        {
            if (player.IsAdmin)
            {
                string msg = args.Trim();
                if (msg.Length == 0)
                {
                    await conn.WriteLineAsync("No message.");
                }
                else
                {
                    msg = "*** " + player.Name + " announces, \"" + msg + "\" ***";
                    foreach (Connection c in _openConnections.Keys)
                    {
                        await c.WriteLineAsync(msg);
                        await c.FlushOutputAsync();
                    }
                }
            }
            else
            {
                await conn.WriteLineAsync("Permission denied.");
            }
        }

        private async Task CmdPageAsync(Connection conn, Player player, string args)
        {
            string target = GetToken(ref args, '=').Trim();
            string msg = args.Trim();

            if (target.Length == 0 || msg.Length == 0)
            {
                await conn.WriteLineAsync("Usage: page <player>=<message>");
                return;
            }

            Player? targetPlayer = await GetPlayerByNameAsync(target.ToLower());
            if (targetPlayer == null)
            {
                await conn.WriteLineAsync("No such player.");
                return;
            }

            if (msg.StartsWith(":", StringComparison.Ordinal))
            {
                msg = msg.Substring(1);

                var sendPage = await WithPlayerConnectionsAsync(targetPlayer, async c =>
                {
                    await c.WriteLineAsync(player.Name + " (paging you) " + msg);
                    await c.FlushOutputAsync();
                });

                if (sendPage)
                {
                    await conn.WriteLineAsync("You page-posed " + targetPlayer.Name + ": " +
                                   player.Name + " " + msg);
                }
                else
                {
                    await conn.WriteLineAsync("That player is not connected.");
                }
            }
            else
            {
                var sendPage = await WithPlayerConnectionsAsync(targetPlayer, async c =>
                {
                    await c.WriteLineAsync(player.Name + " pages: " + msg);
                    await c.FlushOutputAsync();
                });

                if (sendPage)
                {
                    await conn.WriteLineAsync("You paged " + targetPlayer.Name + ": " + msg);
                }
                else
                {
                    await conn.WriteLineAsync("That player is not connected.");
                }
            }
        }

        #region Utility Methods

        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.Days >= 1)
                return string.Format("{0:00}d{1:00}h", timeSpan.Days, timeSpan.Hours);
            else if (timeSpan.Hours >= 1)
                return string.Format("{0:00}h{1:00}m", timeSpan.Hours, timeSpan.Minutes);
            else
                return string.Format("{0:00}m{1:00}s", timeSpan.Minutes, timeSpan.Seconds);
        }

        private static string Sanitize(string str)
        {
            // Sanitize input to prevent special characters from being interpreted
            var sb = new System.Text.StringBuilder(str.Length);
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

        private static string RewriteChatCommandsIfNeeded(string line)
        {
            string lower = line.ToLower();

            // Handle special chat command forms
            if (line.StartsWith('"'))
                line = "$say " + Sanitize(line.Substring(1));
            else if (lower.StartsWith("say ", StringComparison.Ordinal))
                line = "$say " + Sanitize(line.Substring(4));
            else if (line.StartsWith(":", StringComparison.Ordinal))
                line = "$emote " + Sanitize(line.Substring(1));
            else if (lower.StartsWith("pose ", StringComparison.Ordinal))
                line = "$emote " + Sanitize(line.Substring(5));
            else if (lower.StartsWith("emote ", StringComparison.Ordinal))
                line = "$emote " + Sanitize(line.Substring(6));
            else if (line.StartsWith("..", StringComparison.Ordinal))
            {
                // Private say/emote: ..player message
                string[] parts = line.Substring(2).Split(new char[] { ' ' }, 2);
                if (parts.Length < 2)
                    line = Sanitize(line);
                else if (parts[1].StartsWith(":", StringComparison.Ordinal))
                    line = "$emote >" + parts[0] + " " + Sanitize(parts[1].Substring(1));
                else
                    line = "$say >" + parts[0] + " " + Sanitize(parts[1]);
            }
            else
                line = Sanitize(line);
            
            return line;
        }

        #endregion

        #region Event Queue and Timed Events

        /// <summary>
        /// Queues an event to be executed on the event processing thread.
        /// This ensures all game logic is serialized and thread-safe.
        /// </summary>
        public void QueueEvent(Func<Task> eventFunc)
        {
            _eventQueue.Enqueue(eventFunc);
        }

        private async Task ProcessEventsAsync()
        {
            try
            {
                Task<Func<Task>> getQueuedEvent = _eventQueue.DequeueAsync();

                while (_running)
                {
                    var task = await Task.WhenAny(getQueuedEvent, Task.Delay(EVENT_GRANULARITY_MS));

                    if (task == getQueuedEvent)
                    {
                        var eventToRun = await getQueuedEvent;
                        try
                        {
                            await eventToRun();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogMessage(LogLevel.Error, $"Error executing queued event: {ex.Message}");
                            _logger.LogException(ex);
                        }
                        getQueuedEvent = _eventQueue.DequeueAsync();
                    }
                    else
                    {
                        await CheckTimedEventsAsync();
#if !DEBUG
                        CheckWatchdogs();
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, $"Fatal error in event processing loop: {ex.Message}");
                _logger.LogException(ex);
                throw;
            }
        }

        private class TimedEvent
        {
            public readonly IInstance Instance;
            public readonly int Interval;
            public DateTime Time;
            public bool Deleted;

            public TimedEvent(IInstance instance, int interval)
            {
                Instance = instance;
                Time = DateTime.Now.AddSeconds(interval);
                Interval = interval;
            }
        }

        private async Task CheckTimedEventsAsync()
        {
            var now = DateTime.Now;

            while (true)
            {
                var peek = await _timedEvents.TryPeekAsync();

                if (!peek.Success || peek.Item.Time > now)
                {
                    // no item to process
                    break;
                }

                var dq = await _timedEvents.TryDequeueAsync();

                if (!dq.Success)
                {
                    // the peeked item is gone, and now there's nothing more to process
                    break;
                }

                var ev = dq.Item;

                if (ev.Deleted)
                {
                    // discard and try again
                    continue;
                }

                if (ev.Time > now)
                {
                    // the peeked item is gone, and the one we dequeued isn't ready yet
                    await _timedEvents.EnqueueAsync(ev, dq.Priority);
                    break;
                }

                ev.Instance.QueueInput("$rtevent");

                ev.Time = now.AddSeconds(ev.Interval);
                await _timedEvents.EnqueueAsync(ev, ev.Time.ToFileTime());
            }
        }

        private void CheckWatchdogs()
        {
            DateTime now = DateTime.Now;

            foreach (var instance in _instances.Values)
            {
                if (instance is FyreVMInstance fyrevm)
                {
                    if (now >= fyrevm.WatchdogTime)
                    {
                        var failed = fyrevm;
                        QueueEvent(() => HandleInstanceFailureAsync(failed, "frozen"));
                    }
                }
            }
        }

        private async Task HandleInstanceFailureAsync(IInstance instance, string reason)
        {
            _logger.LogMessage(LogLevel.Error, $"Instance '{instance.Realm.Name}' failed: {reason}");

            // Notify all players in the instance
            var playersInInstance = _playerInstances.Where(kvp => kvp.Value == instance).Select(kvp => kvp.Key).ToList();

            foreach (var player in playersInInstance)
            {
                await WithPlayerConnectionsAsync(player, async conn =>
                {
                    await conn.WriteLineAsync($"*** The realm has {reason}. ***");
                    await conn.FlushOutputAsync();
                });

                _playerInstances.TryRemove(player, out _);
            }

            // Remove the instance
            _instances.TryRemove(instance.Realm.Name, out _);
            _timedEventsByInstance.TryRemove(instance, out _);

            // Try to restart it
            try
            {
                var newInstance = await GetDefaultInstanceAsync(instance.Realm);
                
                // Re-enter players
                foreach (var player in playersInInstance)
                {
                    await EnterInstanceAsync(player, newInstance);
                }
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, $"Failed to restart instance: {ex.Message}");
            }
        }

        public async Task ShutdownAsync(string reason)
        {
            _logger.LogMessage(LogLevel.Notice, $"Server shutting down: {reason}");

            // Notify all connected players
            foreach (var conn in _openConnections.Keys)
            {
                try
                {
                    await conn.WriteLineAsync($"*** Server shutting down: {reason} ***");
                    await conn.TerminateAsync();
                }
                catch { }
            }

            // Stop event processing
            _running = false;

            // Wait for event task to complete
            if (_eventTask != null)
            {
                await Task.WhenAny(_eventTask, Task.Delay(5000));
            }

            _logger.LogMessage(LogLevel.Notice, "Server shutdown complete");
        }

        #endregion

        #endregion
    }
}
