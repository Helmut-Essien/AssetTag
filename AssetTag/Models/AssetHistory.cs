using NUlid;

namespace AssetTag.Models
{
    public class AssetHistory
    {
        public string HistoryId { get; set; } = Ulid.NewUlid().ToString();
        public string AssetId { get; set; }
        public string UserId { get; set; }
        public string Action { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public Guid? OldLocationId { get; set; }
        public Guid? NewLocationId { get; set; }
        public string? OldStatus { get; set; }
        public string? NewStatus { get; set; }

        public Asset Asset { get; set; }
        public ApplicationUser User { get; set; }
        public Location? OldLocation { get; set; }
        public Location? NewLocation { get; set; }
    }
}
