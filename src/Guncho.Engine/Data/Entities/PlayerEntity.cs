using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Guncho.Data.Entities;

/// <summary>
/// Database entity representing a player account.
/// </summary>
[Table("Players")]
public class PlayerEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] // IDs are assigned manually
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? PasswordSalt { get; set; }

    [MaxLength(200)]
    public string? PasswordHash { get; set; }

    public bool IsAdmin { get; set; }

    public bool IsGuest { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public virtual ICollection<PlayerAttributeEntity> Attributes { get; set; } = new List<PlayerAttributeEntity>();
    public virtual ICollection<RealmEntity> OwnedRealms { get; set; } = new List<RealmEntity>();
    public virtual ICollection<RealmAccessEntity> RealmAccess { get; set; } = new List<RealmAccessEntity>();
}
