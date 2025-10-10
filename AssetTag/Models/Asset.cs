using NUlid;

namespace AssetTag.Models
{
    public class Asset
    {
        public string AssetId { get; set; } = Ulid.NewUlid().ToString();
        public required string AssetTag { get; set; }
        public string? OldAssetTag { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public required string CategoryId { get; set; }
        public required string LocationId { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchasePrice { get; set; }
        public decimal? CurrentValue { get; set; }
        public required string Status { get; set; }
        public string? AssignedToUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? SerialNumber { get; set; }

        public required Category Category { get; set; }
        public required Location Location { get; set; }
        public ApplicationUser? AssignedToUser { get; set; }
        public required ICollection<AssetHistory> AssetHistories { get; set; }
    }

}
