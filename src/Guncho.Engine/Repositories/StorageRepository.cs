using Guncho.Data;
using Guncho.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Guncho.Repositories;

/// <summary>
/// Repository for realm storage (persistent key-value data) using EF Core.
/// </summary>
public class StorageRepository
{
    private readonly GunchoDbContext _dbContext;

    public StorageRepository(GunchoDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Get realm-scoped storage value.
    /// </summary>
    public async Task<string?> GetRealmValueAsync(int realmId, string key)
    {
        var entry = await _dbContext.StorageEntries
            .FirstOrDefaultAsync(e => e.RealmId == realmId && e.PlayerId == null && e.Key == key);

        return entry?.Value;
    }

    /// <summary>
    /// Get player-scoped storage value within a realm.
    /// </summary>
    public async Task<string?> GetPlayerValueAsync(int realmId, int playerId, string key)
    {
        var entry = await _dbContext.StorageEntries
            .FirstOrDefaultAsync(e => e.RealmId == realmId && e.PlayerId == playerId && e.Key == key);

        return entry?.Value;
    }

    /// <summary>
    /// Get all realm-scoped storage entries for a realm.
    /// </summary>
    public async Task<Dictionary<string, string>> GetAllRealmValuesAsync(int realmId)
    {
        var entries = await _dbContext.StorageEntries
            .Where(e => e.RealmId == realmId && e.PlayerId == null)
            .ToListAsync();

        return entries.ToDictionary(e => e.Key, e => e.Value);
    }

    /// <summary>
    /// Get all player-scoped storage entries for a player in a realm.
    /// </summary>
    public async Task<Dictionary<string, string>> GetAllPlayerValuesAsync(int realmId, int playerId)
    {
        var entries = await _dbContext.StorageEntries
            .Where(e => e.RealmId == realmId && e.PlayerId == playerId)
            .ToListAsync();

        return entries.ToDictionary(e => e.Key, e => e.Value);
    }

    /// <summary>
    /// Set realm-scoped storage value (insert or update).
    /// </summary>
    public async Task SetRealmValueAsync(int realmId, string key, string value)
    {
        var entry = await _dbContext.StorageEntries
            .FirstOrDefaultAsync(e => e.RealmId == realmId && e.PlayerId == null && e.Key == key);

        if (entry == null)
        {
            entry = new StorageEntryEntity
            {
                RealmId = realmId,
                PlayerId = null,
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.StorageEntries.Add(entry);
        }
        else
        {
            entry.Value = value;
            entry.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Set player-scoped storage value (insert or update).
    /// </summary>
    public async Task SetPlayerValueAsync(int realmId, int playerId, string key, string value)
    {
        var entry = await _dbContext.StorageEntries
            .FirstOrDefaultAsync(e => e.RealmId == realmId && e.PlayerId == playerId && e.Key == key);

        if (entry == null)
        {
            entry = new StorageEntryEntity
            {
                RealmId = realmId,
                PlayerId = playerId,
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.StorageEntries.Add(entry);
        }
        else
        {
            entry.Value = value;
            entry.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Delete realm-scoped storage entry.
    /// </summary>
    public async Task DeleteRealmValueAsync(int realmId, string key)
    {
        var entry = await _dbContext.StorageEntries
            .FirstOrDefaultAsync(e => e.RealmId == realmId && e.PlayerId == null && e.Key == key);

        if (entry != null)
        {
            _dbContext.StorageEntries.Remove(entry);
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Delete player-scoped storage entry.
    /// </summary>
    public async Task DeletePlayerValueAsync(int realmId, int playerId, string key)
    {
        var entry = await _dbContext.StorageEntries
            .FirstOrDefaultAsync(e => e.RealmId == realmId && e.PlayerId == playerId && e.Key == key);

        if (entry != null)
        {
            _dbContext.StorageEntries.Remove(entry);
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Delete all storage entries for a realm.
    /// </summary>
    public async Task DeleteAllRealmStorageAsync(int realmId)
    {
        var entries = await _dbContext.StorageEntries
            .Where(e => e.RealmId == realmId)
            .ToListAsync();

        _dbContext.StorageEntries.RemoveRange(entries);
        await _dbContext.SaveChangesAsync();
    }
}
