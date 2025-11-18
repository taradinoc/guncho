using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Guncho.Data.Entities;

/// <summary>
/// Database entity representing an Inform 7 realm (shared world).
/// </summary>
[Table("Realms")]
public class RealmEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int OwnerId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Privacy { get; set; } = "public"; // "public", "joinable", "private"

    [Required]
    [MaxLength(50)]
    public string Factory { get; set; } = "5Z71"; // Inform 7 compiler version

    /// <summary>
    /// The name of the asset that should be passed to the compiler as the main file.
    /// For Inform 7: typically "story.ni"
    /// For Inform 6: the main .inf file (e.g., "main.inf")
    /// If null, defaults to factory-specific convention.
    /// </summary>
    [MaxLength(200)]
    public string? MainFile { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastCompiledAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(OwnerId))]
    public virtual PlayerEntity Owner { get; set; } = null!;

    public virtual ICollection<RealmAssetEntity> Assets { get; set; } = new List<RealmAssetEntity>();
    public virtual ICollection<RealmAccessEntity> AccessList { get; set; } = new List<RealmAccessEntity>();
    public virtual ICollection<StorageEntryEntity> StorageEntries { get; set; } = new List<StorageEntryEntity>();
}
