using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Guncho.Data.Entities;

/// <summary>
/// Database entity for player attributes (pronouns, description, etc.).
/// </summary>
[Table("PlayerAttributes")]
public class PlayerAttributeEntity
{
    [Key]
    public int Id { get; set; }

    public int PlayerId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    // Navigation property
    [ForeignKey(nameof(PlayerId))]
    public virtual PlayerEntity Player { get; set; } = null!;
}
