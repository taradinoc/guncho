using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Guncho.Data.Entities;

/// <summary>
/// Database entity for realm access control lists (ACLs).
/// </summary>
[Table("RealmAccess")]
public class RealmAccessEntity
{
    [Key]
    public int Id { get; set; }

    public int RealmId { get; set; }

    public int PlayerId { get; set; }

    [Required]
    [MaxLength(50)]
    public string AccessLevel { get; set; } = string.Empty; // "banned", "hidden", "visible", "invited", "view_source", "full_control"

    // Navigation properties
    [ForeignKey(nameof(RealmId))]
    public virtual RealmEntity Realm { get; set; } = null!;

    [ForeignKey(nameof(PlayerId))]
    public virtual PlayerEntity Player { get; set; } = null!;
}
