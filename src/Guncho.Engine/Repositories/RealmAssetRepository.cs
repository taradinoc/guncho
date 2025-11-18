using Guncho.Data;
using Guncho.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Guncho.Repositories;

/// <summary>
/// Repository for realm asset data using EF Core.
/// </summary>
public class RealmAssetRepository
{
    private readonly GunchoDbContext _dbContext;

    public RealmAssetRepository(GunchoDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Get an asset by realm ID and asset name.
    /// </summary>
    public async Task<RealmAssetEntity?> GetByNameAsync(int realmId, string name)
    {
        return await _dbContext.RealmAssets
            .FirstOrDefaultAsync(a => a.RealmId == realmId && a.Name == name);
    }

    /// <summary>
    /// Get all assets for a realm (without loading content into memory).
    /// </summary>
    public async Task<List<RealmAssetInfo>> GetAllForRealmAsync(int realmId)
    {
        return await _dbContext.RealmAssets
            .Where(a => a.RealmId == realmId)
            .Select(a => new RealmAssetInfo
            {
                Id = a.Id,
                RealmId = a.RealmId,
                Name = a.Name,
                ContentType = a.ContentType,
                Size = a.Content.Length,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            })
            .ToListAsync();
    }

    /// <summary>
    /// Get all assets with their content for a realm (for compilation).
    /// Returns a dictionary of asset name -> content bytes.
    /// </summary>
    public async Task<Dictionary<string, byte[]>> GetAllContentForRealmAsync(int realmId)
    {
        var assets = await _dbContext.RealmAssets
            .Where(a => a.RealmId == realmId)
            .Select(a => new { a.Name, a.Content })
            .ToListAsync();
        
        return assets.ToDictionary(a => a.Name, a => a.Content);
    }

    /// <summary>
    /// Get asset content as bytes.
    /// </summary>
    public async Task<byte[]?> GetContentAsync(int realmId, string name)
    {
        var asset = await _dbContext.RealmAssets
            .FirstOrDefaultAsync(a => a.RealmId == realmId && a.Name == name);
        
        return asset?.Content;
    }

    /// <summary>
    /// Get asset content as string (assumes UTF-8 encoding).
    /// </summary>
    public async Task<string?> GetContentAsStringAsync(int realmId, string name)
    {
        var content = await GetContentAsync(realmId, name);
        return content != null ? System.Text.Encoding.UTF8.GetString(content) : null;
    }

    /// <summary>
    /// Create or update an asset.
    /// </summary>
    public async Task<int> SaveAsync(int realmId, string name, byte[] content, string contentType = "text/plain")
    {
        var existing = await _dbContext.RealmAssets
            .FirstOrDefaultAsync(a => a.RealmId == realmId && a.Name == name);

        if (existing == null)
        {
            var asset = new RealmAssetEntity
            {
                RealmId = realmId,
                Name = name,
                ContentType = contentType,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.RealmAssets.Add(asset);
            await _dbContext.SaveChangesAsync();
            return asset.Id;
        }
        else
        {
            existing.Content = content;
            existing.ContentType = contentType;
            existing.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return existing.Id;
        }
    }

    /// <summary>
    /// Delete an asset.
    /// </summary>
    public async Task DeleteAsync(int realmId, string name)
    {
        var asset = await _dbContext.RealmAssets
            .FirstOrDefaultAsync(a => a.RealmId == realmId && a.Name == name);

        if (asset != null)
        {
            _dbContext.RealmAssets.Remove(asset);
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Delete all assets for a realm.
    /// </summary>
    public async Task DeleteAllForRealmAsync(int realmId)
    {
        var assets = await _dbContext.RealmAssets
            .Where(a => a.RealmId == realmId)
            .ToListAsync();

        _dbContext.RealmAssets.RemoveRange(assets);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Check if an asset exists.
    /// </summary>
    public async Task<bool> ExistsAsync(int realmId, string name)
    {
        return await _dbContext.RealmAssets
            .AnyAsync(a => a.RealmId == realmId && a.Name == name);
    }
}

/// <summary>
/// Lightweight asset information (without content).
/// </summary>
public class RealmAssetInfo
{
    public int Id { get; set; }
    public int RealmId { get; set; }
    public required string Name { get; set; }
    public required string ContentType { get; set; }
    public int Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
