using Guncho.Data;
using Guncho.Data.Entities;
using Guncho.Services;
using Microsoft.EntityFrameworkCore;

namespace Guncho.Repositories;

/// <summary>
/// Repository for realm data using EF Core.
/// </summary>
public class RealmRepository
{
    private readonly GunchoDbContext _dbContext;

    public RealmRepository(GunchoDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<RealmMetadata?> GetByNameAsync(string name)
    {
        var entity = await _dbContext.Realms
            .Include(r => r.Owner)
                .Include(r => r.Assets)
            .Include(r => r.AccessList)
            .FirstOrDefaultAsync(r => r.Name == name);

        return entity != null ? MapToRealmMetadata(entity) : null;
    }

    public async Task<RealmMetadata?> GetByIdAsync(int id)
    {
        var entity = await _dbContext.Realms
            .Include(r => r.Owner)
                .Include(r => r.Assets)
            .Include(r => r.AccessList)
            .FirstOrDefaultAsync(r => r.Id == id);

        return entity != null ? MapToRealmMetadata(entity) : null;
    }

    public async Task<IEnumerable<RealmMetadata>> GetAllAsync()
    {
        var entities = await _dbContext.Realms
            .Include(r => r.Owner)
                .Include(r => r.Assets)
            .Include(r => r.AccessList)
            .ToListAsync();

        return entities.Select(MapToRealmMetadata);
    }

    public async Task<int> SaveAsync(RealmMetadata metadata)
    {
        var existing = await _dbContext.Realms
            .Include(r => r.AccessList)
            .FirstOrDefaultAsync(r => r.Name == metadata.Name);

        if (existing == null)
        {
            // Create new realm
            var entity = new RealmEntity
            {
                Name = metadata.Name,
                OwnerId = metadata.OwnerId,
                Privacy = metadata.Privacy,
                Factory = metadata.Factory,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Realms.Add(entity);
            await _dbContext.SaveChangesAsync();

            // Add ACLs
            foreach (var acl in metadata.AccessList)
            {
                var aclEntity = new RealmAccessEntity
                {
                    RealmId = entity.Id,
                    PlayerId = acl.PlayerId,
                    AccessLevel = acl.AccessLevel
                };
                _dbContext.RealmAccess.Add(aclEntity);
            }

            await _dbContext.SaveChangesAsync();
            return entity.Id;
        }
        else
        {
            // Update existing realm
            existing.Privacy = metadata.Privacy;
            existing.Factory = metadata.Factory;

            // Update ACLs - remove old and add new
            _dbContext.RealmAccess.RemoveRange(existing.AccessList);

            foreach (var acl in metadata.AccessList)
            {
                var aclEntity = new RealmAccessEntity
                {
                    RealmId = existing.Id,
                    PlayerId = acl.PlayerId,
                    AccessLevel = acl.AccessLevel
                };
                _dbContext.RealmAccess.Add(aclEntity);
            }

            await _dbContext.SaveChangesAsync();
            return existing.Id;
        }
    }

    public async Task UpdateLastCompiledAsync(int realmId)
    {
        var entity = await _dbContext.Realms.FindAsync(realmId);
        if (entity != null)
        {
            entity.LastCompiledAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(string realmName)
    {
        var entity = await _dbContext.Realms.FirstOrDefaultAsync(r => r.Name == realmName);
        if (entity != null)
        {
            _dbContext.Realms.Remove(entity);
            await _dbContext.SaveChangesAsync();
        }
    }

    private static RealmMetadata MapToRealmMetadata(RealmEntity entity)
    {
        return new RealmMetadata
        {
            Id = entity.Id,
            Name = entity.Name,
            OwnerId = entity.OwnerId,
            OwnerName = entity.Owner.Name,
            Privacy = entity.Privacy,
            Factory = entity.Factory,
            MainFile = entity.MainFile,
                Assets = entity.Assets.Select(a => new RealmAssetMetadata
                {
                    Id = a.Id,
                    Name = a.Name,
                    ContentType = a.ContentType,
                    Size = a.Content.Length,
                    UpdatedAt = a.UpdatedAt
                }).ToList(),
            AccessList = entity.AccessList.Select(a => new RealmAccessMetadata
            {
                PlayerId = a.PlayerId,
                AccessLevel = a.AccessLevel
            }).ToList()
        };
    }

    public async Task SetMainFileAsync(int realmId, string mainFileName)
    {
        var entity = await _dbContext.Realms.FindAsync(realmId);
        if (entity != null)
        {
            entity.MainFile = mainFileName;
            await _dbContext.SaveChangesAsync();
        }
    }
}

/// <summary>
/// Lightweight metadata for realm (doesn't include full Realm object with VM).
/// </summary>
public class RealmMetadata
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string Privacy { get; set; } = "public";
    public string Factory { get; set; } = "5Z71";
    public string? MainFile { get; set; }
        public List<RealmAssetMetadata> Assets { get; set; } = new();
    public List<RealmAccessMetadata> AccessList { get; set; } = new();
}
    /// <summary>
    /// Lightweight metadata for realm asset (doesn't include content).
    /// </summary>
    public class RealmAssetMetadata
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string ContentType { get; set; }
        public int Size { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

/// <summary>
/// Lightweight metadata for realm ACL entry.
/// </summary>
public class RealmAccessMetadata
{
    public int PlayerId { get; set; }
    public required string AccessLevel { get; set; }
}
