using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Guncho.Shared.Models
{
    /// <summary>
    /// Describes the level of access that a player has to a realm.
    /// </summary>
    public enum RealmAccessLevel
    {
        Invalid,
        Banned,
        Hidden,
        Visible,
        Invited,
        ViewSource,
        EditSource,
        EditSettings,
        EditAccess,
        SafetyOff
    }

    /// <summary>
    /// Describes the default visibility and accessibility of a realm
    /// to players other than the owner.
    /// </summary>
    public enum RealmPrivacyLevel
    {
        Private,
        Hidden,
        Public,
        Joinable,
        Viewable
    }

    public class RealmAccessListEntryDto
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public RealmAccessLevel Level { get; set; }
    }

    public class RealmDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        public int Id { get; set; }

        public int OwnerId { get; set; }
        
        public string OwnerName { get; set; } = string.Empty;
        
        public RealmPrivacyLevel PrivacyLevel { get; set; }
        
        public List<RealmAccessListEntryDto> AccessList { get; set; } = new();
        
        public string FactoryName { get; set; } = string.Empty;
        
        public bool IsCondemned { get; set; }
    }
    
    public class RealmSummaryDto
    {
        public string Name { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public RealmPrivacyLevel PrivacyLevel { get; set; }
        public string FactoryName { get; set; } = string.Empty;
    }
    
    public class CreateRealmDto
    {
        [Required]
        [RegularExpression(@"^[a-zA-Z][-a-zA-Z0-9_ ]*$", ErrorMessage = "Realm name must start with a letter and contain only letters, numbers, spaces, hyphens, and underscores.")]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string FactoryName { get; set; } = string.Empty;
        
        public RealmPrivacyLevel PrivacyLevel { get; set; } = RealmPrivacyLevel.Public;
    }
    
    public class RealmFactoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class RealmAssetSummaryDto
    {
        [Required]
        public string Path { get; set; } = string.Empty;

        public string ContentType { get; set; } = "text/plain";

        public int Version { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class RealmAssetDto : RealmAssetSummaryDto
    {
        [Required]
        public string Content { get; set; } = string.Empty;
    }

    public class RealmAssetUpdateDto
    {
        [Required]
        public string Content { get; set; } = string.Empty;

        public string ContentType { get; set; } = "text/plain";

        /// <summary>
        /// Optional optimistic concurrency check. If provided, update will fail when the stored version differs.
        /// </summary>
        public int? ExpectedVersion { get; set; }
    }
}
