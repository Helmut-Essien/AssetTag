using NUlid;

namespace Shared.Models
{
    /// <summary>
    /// FIX #5: Tracks deleted entities for sync purposes
    /// When an entity is deleted on the server, we record it here so mobile clients
    /// can learn about the deletion during their next pull sync
    /// </summary>
    public class DeletedItem
    {
        public string DeletedItemId { get; set; } = Ulid.NewUlid().ToString();
        
        /// <summary>
        /// Type of entity that was deleted (e.g., "Asset", "Category", "Location", "Department")
        /// </summary>
        public required string EntityType { get; set; }
        
        /// <summary>
        /// ID of the entity that was deleted
        /// </summary>
        public required string EntityId { get; set; }
        
        /// <summary>
        /// When the entity was deleted (UTC)
        /// </summary>
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Optional: User who performed the deletion
        /// </summary>
        public string? DeletedByUserId { get; set; }
        
        /// <summary>
        /// Optional: Reason for deletion or additional context
        /// </summary>
        public string? Reason { get; set; }
    }
}

// Made with Bob
