using Guncho.Data;
using Guncho.Data.Entities;
using Guncho.Services;
using Microsoft.EntityFrameworkCore;

namespace Guncho.Repositories;

/// <summary>
/// Repository for player data using EF Core.
/// </summary>
public class PlayerRepository
{
    private readonly GunchoDbContext _dbContext;

    public PlayerRepository(GunchoDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Player?> GetByNameAsync(string name)
    {
        var entity = await _dbContext.Players
            .Include(p => p.Attributes)
            .FirstOrDefaultAsync(p => p.Name == name);

        return entity != null ? MapToPlayer(entity) : null;
    }

    public async Task<Player?> GetByIdAsync(int id)
    {
        var entity = await _dbContext.Players
            .Include(p => p.Attributes)
            .FirstOrDefaultAsync(p => p.Id == id);

        return entity != null ? MapToPlayer(entity) : null;
    }

    public async Task<IEnumerable<Player>> GetAllAsync()
    {
        var entities = await _dbContext.Players
            .Include(p => p.Attributes)
            .ToListAsync();

        return entities.Select(MapToPlayer);
    }

    public async Task SaveAsync(Player player)
    {
        var existing = await _dbContext.Players
            .Include(p => p.Attributes)
            .FirstOrDefaultAsync(p => p.Id == player.ID);

        if (existing == null)
        {
            // Create new player
            var entity = new PlayerEntity
            {
                Id = player.ID,
                Name = player.Name,
                PasswordSalt = player.PasswordSalt,
                PasswordHash = player.PasswordHash,
                IsAdmin = player.IsAdmin,
                IsGuest = player.IsGuest,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Players.Add(entity);
            existing = entity;
        }
        else
        {
            // Update existing player
            existing.Name = player.Name;
            existing.PasswordSalt = player.PasswordSalt;
            existing.PasswordHash = player.PasswordHash;
        }

        // Update attributes - remove old ones and add new ones
        _dbContext.PlayerAttributes.RemoveRange(existing.Attributes);

        foreach (var (key, value) in player.GetAllAttributes())
        {
            var attrEntity = new PlayerAttributeEntity
            {
                PlayerId = player.ID,
                Name = key,
                Value = value
            };
            _dbContext.PlayerAttributes.Add(attrEntity);
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(Player player)
    {
        var entity = await _dbContext.Players.FindAsync(player.ID);
        if (entity != null)
        {
            _dbContext.Players.Remove(entity);
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task UpdateLastLoginAsync(int playerId)
    {
        var entity = await _dbContext.Players.FindAsync(playerId);
        if (entity != null)
        {
            entity.LastLoginAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }

    private static Player MapToPlayer(PlayerEntity entity)
    {
        var player = new Player(entity.Id, entity.Name, entity.IsAdmin, entity.IsGuest)
        {
            PasswordSalt = entity.PasswordSalt,
            PasswordHash = entity.PasswordHash
        };

        foreach (var attr in entity.Attributes)
        {
            player.SetAttribute(attr.Name, attr.Value);
        }

        return player;
    }
}
