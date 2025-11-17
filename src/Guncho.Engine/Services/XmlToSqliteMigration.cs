using Guncho.Data;
using Guncho.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Xml.Serialization;
using XML = Guncho.XML;

namespace Guncho.Services;

/// <summary>
/// One-time migration utility to import data from XML files to SQLite database.
/// </summary>
public class XmlToSqliteMigration
{
    private readonly GunchoDbContext _dbContext;
    private readonly string _realmDataPath;
    private readonly ILogger _logger;

    public XmlToSqliteMigration(GunchoDbContext dbContext, string realmDataPath, ILogger logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _realmDataPath = realmDataPath ?? throw new ArgumentNullException(nameof(realmDataPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Performs the complete migration from XML to SQLite.
    /// </summary>
    public async Task MigrateAsync()
    {
        _logger.LogMessage(LogLevel.Notice, "Starting XML to SQLite migration...");

        // Ensure database is created
        await _dbContext.Database.EnsureCreatedAsync();

        // Check if migration already done
        if (await _dbContext.Players.AnyAsync())
        {
            _logger.LogMessage(LogLevel.Warning, "Database already contains data. Skipping migration.");
            return;
        }

        // Migrate in order: Players -> Realms -> ACLs -> Storage
        await MigratePlayersAsync();
        await MigrateRealmsAsync();
        await MigrateRealmStorageAsync();

        _logger.LogMessage(LogLevel.Notice, "Migration completed successfully!");
    }

    private async Task MigratePlayersAsync()
    {
        var playerIndexPath = Path.Combine(_realmDataPath, "playerIndex.xml");
        if (!File.Exists(playerIndexPath))
        {
            _logger.LogMessage(LogLevel.Warning, $"playerIndex.xml not found at {playerIndexPath}");
            return;
        }

        try
        {
            var serializer = new XmlSerializer(typeof(XML.playerIndex));
            using var fs = new FileStream(playerIndexPath, FileMode.Open, FileAccess.Read);
            var index = (XML.playerIndex?)serializer.Deserialize(fs);

            if (index?.Item?.player == null)
            {
                _logger.LogMessage(LogLevel.Warning, "No players found in playerIndex.xml");
                return;
            }

            var playerCount = 0;
            var attributeCount = 0;
            var seenPlayerIds = new HashSet<int>();

            foreach (var xmlPlayer in index.Item.player)
            {
                // Skip duplicate player IDs
                if (seenPlayerIds.Contains(xmlPlayer.id))
                {
                    _logger.LogMessage(LogLevel.Warning, $"Duplicate player ID {xmlPlayer.id} ('{xmlPlayer.name}'). Skipping duplicate entry.");
                    continue;
                }
                seenPlayerIds.Add(xmlPlayer.id);

                var playerEntity = new PlayerEntity
                {
                    Id = xmlPlayer.id,
                    Name = xmlPlayer.name,
                    PasswordSalt = xmlPlayer.pwdSalt,
                    PasswordHash = xmlPlayer.pwdHash,
                    IsAdmin = xmlPlayer.admin || xmlPlayer.adminSpecified,
                    IsGuest = false,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Players.Add(playerEntity);

                // Migrate attributes
                if (xmlPlayer.attribute != null)
                {
                    foreach (var attr in xmlPlayer.attribute)
                    {
                        var attrEntity = new PlayerAttributeEntity
                        {
                            PlayerId = xmlPlayer.id,
                            Name = attr.name,
                            Value = attr.Value
                        };
                        _dbContext.PlayerAttributes.Add(attrEntity);
                        attributeCount++;
                    }
                }

                playerCount++;
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogMessage(LogLevel.Notice, $"Migrated {playerCount} players with {attributeCount} attributes");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
            throw new InvalidOperationException("Failed to migrate players from XML", ex);
        }
    }

    private async Task MigrateRealmsAsync()
    {
        var realmIndexPath = Path.Combine(_realmDataPath, "realmIndex.xml");
        if (!File.Exists(realmIndexPath))
        {
            _logger.LogMessage(LogLevel.Warning, $"realmIndex.xml not found at {realmIndexPath}");
            return;
        }

        try
        {
            var serializer = new XmlSerializer(typeof(XML.realmIndex));
            using var fs = new FileStream(realmIndexPath, FileMode.Open, FileAccess.Read);
            var index = (XML.realmIndex?)serializer.Deserialize(fs);

            if (index?.realms == null)
            {
                _logger.LogMessage(LogLevel.Warning, "No realms found in realmIndex.xml");
                return;
            }

            var realmCount = 0;
            var aclCount = 0;

            foreach (var xmlRealm in index.realms)
            {
                // Look up owner by name
                var ownerEntity = await _dbContext.Players
                    .FirstOrDefaultAsync(p => p.Name == xmlRealm.owner);

                if (ownerEntity == null)
                {
                    _logger.LogMessage(LogLevel.Warning, $"Owner '{xmlRealm.owner}' not found for realm '{xmlRealm.name}'. Skipping realm.");
                    continue;
                }

                var realmEntity = new RealmEntity
                {
                    Name = xmlRealm.name,
                    OwnerId = ownerEntity.Id,
                    Privacy = xmlRealm.privacy.ToString(),
                    Factory = xmlRealm.factory ?? "5Z71",
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Realms.Add(realmEntity);
                await _dbContext.SaveChangesAsync(); // Save to get RealmId

                    // Import source file as an asset
                    var sourceFileName = xmlRealm.src ?? $"{xmlRealm.name}.ni";
                    var sourceFilePath = Path.Combine(_realmDataPath, sourceFileName);
                
                    if (File.Exists(sourceFilePath))
                    {
                        var sourceContent = await File.ReadAllBytesAsync(sourceFilePath);
                        var assetEntity = new RealmAssetEntity
                        {
                            RealmId = realmEntity.Id,
                            Name = "story.ni",
                            ContentType = "text/plain",
                            Content = sourceContent,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _dbContext.RealmAssets.Add(assetEntity);
                        await _dbContext.SaveChangesAsync();
                    }
                    else
                    {
                        _logger.LogMessage(LogLevel.Warning, $"Source file '{sourceFileName}' not found for realm '{xmlRealm.name}'. Realm created without assets.");
                    }

                // Migrate ACLs
                if (xmlRealm.access != null)
                {
                    foreach (var xmlAccess in xmlRealm.access)
                    {
                        var playerEntity = await _dbContext.Players
                            .FirstOrDefaultAsync(p => p.Name == xmlAccess.player);

                        if (playerEntity == null)
                        {
                            _logger.LogMessage(LogLevel.Warning, $"Player '{xmlAccess.player}' not found for ACL in realm '{xmlRealm.name}'. Skipping ACL entry.");
                            continue;
                        }

                        var accessEntity = new RealmAccessEntity
                        {
                            RealmId = realmEntity.Id,
                            PlayerId = playerEntity.Id,
                            AccessLevel = xmlAccess.level.ToString()
                        };

                        _dbContext.RealmAccess.Add(accessEntity);
                        aclCount++;
                    }
                }

                realmCount++;
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogMessage(LogLevel.Notice, $"Migrated {realmCount} realms with {aclCount} ACL entries");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
            throw new InvalidOperationException("Failed to migrate realms from XML", ex);
        }
    }

    private async Task MigrateRealmStorageAsync()
    {
        var realms = await _dbContext.Realms.ToListAsync();
        var totalStorageCount = 0;

        foreach (var realm in realms)
        {
            var storagePath = Path.Combine(_realmDataPath, $"{realm.Name}.storage.xml");
            if (!File.Exists(storagePath))
            {
                continue; // Storage file is optional
            }

            try
            {
                var serializer = new XmlSerializer(typeof(XML.realmStorage));
                using var fs = new FileStream(storagePath, FileMode.Open, FileAccess.Read);
                var storage = (XML.realmStorage?)serializer.Deserialize(fs);

                if (storage == null)
                    continue;

                var storageCount = 0;

                // Migrate realm-scoped storage items
                if (storage.item != null)
                {
                    foreach (var item in storage.item)
                    {
                        var storageEntity = new StorageEntryEntity
                        {
                            RealmId = realm.Id,
                            PlayerId = null, // Realm-scoped
                            Key = item.key,
                            Value = item.Value ?? "",
                            UpdatedAt = DateTime.UtcNow
                        };
                        _dbContext.StorageEntries.Add(storageEntity);
                        storageCount++;
                    }
                }

                // Migrate player-scoped storage items
                if (storage.player != null)
                {
                    foreach (var xmlPlayer in storage.player)
                    {
                        var playerEntity = await _dbContext.Players
                            .FirstOrDefaultAsync(p => p.Name == xmlPlayer.name);

                        if (playerEntity == null)
                        {
                            _logger.LogMessage(LogLevel.Warning, $"Player '{xmlPlayer.name}' not found for storage in realm '{realm.Name}'. Skipping player storage.");
                            continue;
                        }

                        if (xmlPlayer.item != null)
                        {
                            foreach (var item in xmlPlayer.item)
                            {
                                var storageEntity = new StorageEntryEntity
                                {
                                    RealmId = realm.Id,
                                    PlayerId = playerEntity.Id,
                                    Key = item.key,
                                    Value = item.Value ?? "",
                                    UpdatedAt = DateTime.UtcNow
                                };
                                _dbContext.StorageEntries.Add(storageEntity);
                                storageCount++;
                            }
                        }
                    }
                }

                await _dbContext.SaveChangesAsync();
                _logger.LogMessage(LogLevel.Verbose, $"Migrated {storageCount} storage entries for realm '{realm.Name}'");
                totalStorageCount += storageCount;
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, $"Failed to migrate storage for realm '{realm.Name}': {ex.Message}");
            }
        }

        _logger.LogMessage(LogLevel.Notice, $"Migrated {totalStorageCount} total storage entries");
    }
}
