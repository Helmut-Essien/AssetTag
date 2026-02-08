using NUlid;
using System.ComponentModel.DataAnnotations.Schema;

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

        // Vendor and Invoice Information
        public string? VendorName { get; set; }
        public string? InvoiceNumber { get; set; }
        
        // Base Financial Fields (Stored in Database)
        public int Quantity { get; set; } = 1;
        public decimal? CostPerUnit { get; set; }
        public int? UsefulLifeYears { get; set; }
        
        // Calculated Financial Fields (Not Stored - Computed Properties)
        /// <summary>
        /// Total Cost = Cost Per Unit × Quantity
        /// </summary>
        [NotMapped]
        public decimal? TotalCost => CostPerUnit.HasValue ? CostPerUnit.Value * Quantity : null;

        /// <summary>
        /// Accumulated Depreciation calculated using straight-line method.
        /// Uses the depreciation rate from the asset's Category.
        /// Formula: (Purchase Price × Category Depreciation Rate × Years Owned) capped at Purchase Price
        /// </summary>
        [NotMapped]
        public decimal? AccumulatedDepreciation
        {
            get
            {
                if (!PurchasePrice.HasValue || !PurchaseDate.HasValue || Category?.DepreciationRate == null)
                    return null;

                var yearsOwned = (DateTime.UtcNow - PurchaseDate.Value).TotalDays / 365.25;
                var rate = Category.DepreciationRate.Value / 100m;
                var calculated = PurchasePrice.Value * rate * (decimal)yearsOwned;
                
                // Cap at purchase price (cannot depreciate more than original value)
                return Math.Min(calculated, PurchasePrice.Value);
            }
        }

        /// <summary>
        /// Net Book Value = Purchase Price - Accumulated Depreciation
        /// Minimum value is 0 (cannot be negative)
        /// </summary>
        [NotMapped]
        public decimal? NetBookValue
        {
            get
            {
                if (!PurchasePrice.HasValue)
                    return null;

                var accumulated = AccumulatedDepreciation ?? 0m;
                return Math.Max(0m, PurchasePrice.Value - accumulated);
            }
        }

        // Other Fields
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