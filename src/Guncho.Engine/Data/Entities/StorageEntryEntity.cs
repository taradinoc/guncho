using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Guncho.Data.Entities;

/// <summary>
/// Database entity for realm storage (persistent key-value data for game state).
/// Can be realm-scoped or player-scoped within a realm.
/// </summary>
[Table("StorageEntries")]
public class StorageEntryEntity
{
    [Key]
    public int Id { get; set; }

    public int RealmId { get; set; }

    /// <summary>
    /// Null for realm-scoped storage, PlayerId for player-scoped storage.
    /// </summary>
    public int? PlayerId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(RealmId))]
    public virtual RealmEntity Realm { get; set; } = null!;

    [ForeignKey(nameof(PlayerId))]
    public virtual PlayerEntity? Player { get; set; }
}
