using NUlid;

namespace Shared.Models
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
        public required string DepartmentId { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchasePrice { get; set; }
        public decimal? CurrentValue { get; set; }
        public required string Status { get; set; }
        public string? AssignedToUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime DateModified { get; set; } = DateTime.UtcNow;
        public string? SerialNumber { get; set; }
        public required string Condition { get; set; }

        // New fields from Excel
        public string? VendorName { get; set; }
        public string? InvoiceNumber { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal? CostPerUnit { get; set; }
        public decimal? TotalCost { get; set; }
        public decimal? DepreciationRate { get; set; }
        public decimal? AccumulatedDepreciation { get; set; }
        public decimal? NetBookValue { get; set; }
        public int? UsefulLifeYears { get; set; }
        public DateTime? WarrantyExpiry { get; set; }
        public DateTime? DisposalDate { get; set; }
        public decimal? DisposalValue { get; set; }
        public string? Remarks { get; set; }

        // Navigation properties
        public Category? Category { get; set; }
        public Location? Location { get; set; }
        public  Department? Department { get; set; }
        public ApplicationUser? AssignedToUser { get; set; }
        public ICollection<AssetHistory> AssetHistories { get; set; } = new List<AssetHistory>();
    }
}