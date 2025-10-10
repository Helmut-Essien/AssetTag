namespace AssetTag.Models
{
    public class Asset
    {
        public Guid AssetId { get; set; }
        public string AssetTag { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Guid CategoryId { get; set; }
        public Guid LocationId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal CurrentValue { get; set; }
        public string Status { get; set; }
        public string? AssignedToUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? SerialNumber { get; set; }

        public Category Category { get; set; }
        public Location Location { get; set; }
        public ApplicationUser? AssignedToUser { get; set; }
        public ICollection<AssetHistory> AssetHistories { get; set; }
    }

}
