using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Guncho.Data.Entities;

/// <summary>
/// Database entity for realm assets (source files, resources, etc.).
/// Each realm can have multiple assets (e.g., story.ni, images, sounds).
/// </summary>
[Table("RealmAssets")]
public class RealmAssetEntity
{
    [Key]
    public int Id { get; set; }

    public int RealmId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = "text/plain"; // MIME type (e.g., "text/plain", "image/png")

    [Required]
    public byte[] Content { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey(nameof(RealmId))]
    public virtual RealmEntity Realm { get; set; } = null!;
}
