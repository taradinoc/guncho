using Guncho.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Guncho.Data;

/// <summary>
/// Entity Framework Core database context for Guncho.
/// </summary>
public class GunchoDbContext : DbContext
{
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<PlayerAttributeEntity> PlayerAttributes => Set<PlayerAttributeEntity>();
    public DbSet<RealmEntity> Realms => Set<RealmEntity>();
    public DbSet<RealmAssetEntity> RealmAssets => Set<RealmAssetEntity>();
    public DbSet<RealmAccessEntity> RealmAccess => Set<RealmAccessEntity>();
    public DbSet<StorageEntryEntity> StorageEntries => Set<StorageEntryEntity>();

    public GunchoDbContext(DbContextOptions<GunchoDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Player entity configuration
        modelBuilder.Entity<PlayerEntity>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).UseCollation("NOCASE"); // Case-insensitive
        });

        // PlayerAttribute entity configuration
        modelBuilder.Entity<PlayerAttributeEntity>(entity =>
        {
            entity.HasIndex(e => new { e.PlayerId, e.Name }).IsUnique();
        });

        // Realm entity configuration
        modelBuilder.Entity<RealmEntity>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).UseCollation("NOCASE"); // Case-insensitive
            
            entity.HasOne(e => e.Owner)
                .WithMany(p => p.OwnedRealms)
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Restrict); // Don't cascade delete realms if owner deleted
        });

        // RealmAsset entity configuration
        modelBuilder.Entity<RealmAssetEntity>(entity =>
        {
            entity.HasIndex(e => new { e.RealmId, e.Name }).IsUnique();
            
            entity.HasOne(e => e.Realm)
                .WithMany(r => r.Assets)
                .HasForeignKey(e => e.RealmId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RealmAccess entity configuration
        modelBuilder.Entity<RealmAccessEntity>(entity =>
        {
            entity.HasIndex(e => new { e.RealmId, e.PlayerId }).IsUnique();
            
            entity.HasOne(e => e.Realm)
                .WithMany(r => r.AccessList)
                .HasForeignKey(e => e.RealmId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Player)
                .WithMany(p => p.RealmAccess)
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // StorageEntry entity configuration
        modelBuilder.Entity<StorageEntryEntity>(entity =>
        {
            // Unique constraint: realm + player + key
            entity.HasIndex(e => new { e.RealmId, e.PlayerId, e.Key }).IsUnique();
            
            entity.HasOne(e => e.Realm)
                .WithMany(r => r.StorageEntries)
                .HasForeignKey(e => e.RealmId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
