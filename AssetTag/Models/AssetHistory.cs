using NUlid;

namespace AssetTag.Models
{
    public class AssetHistory
    {
        public string HistoryId { get; set; } = Ulid.NewUlid().ToString();
        public required string AssetId { get; set; }
        public required string UserId { get; set; }
        public required string Action { get; set; }
        public required string Description { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? OldLocationId { get; set; }
        public string? NewLocationId { get; set; }
        public string? OldStatus { get; set; }
        public string? NewStatus { get; set; }

        public  Asset Asset { get; set; }
        public  ApplicationUser User { get; set; }
        public Location? OldLocation { get; set; }
        public Location? NewLocation { get; set; }
    }
}