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
        public int? UsefulLifeYears { get; set; }  // Optional: User can override calculated useful life
        
        // Calculated Financial Fields (Not Stored - Computed Properties)
        /// <summary>
        /// Calculated Useful Life based on Category Depreciation Rate.
        /// Formula: 100 / Depreciation Rate = Useful Life Years
        /// Example: 20% depreciation rate = 5 years useful life
        /// Returns user-specified UsefulLifeYears if set, otherwise calculates from depreciation rate.
        /// </summary>
        [NotMapped]
        public int? CalculatedUsefulLifeYears
        {
            get
            {
                // If user specified useful life, use that
                if (UsefulLifeYears.HasValue)
                    return UsefulLifeYears.Value;
                
                // Otherwise calculate from depreciation rate
                if (Category?.DepreciationRate == null || Category.DepreciationRate.Value == 0)
                    return null;
                
                return (int)Math.Ceiling(100m / Category.DepreciationRate.Value);
            }
        }
        
        /// <summary>
        /// Total Cost = Cost Per Unit × Quantity
        /// </summary>
        [NotMapped]
        public decimal? TotalCost => CostPerUnit.HasValue ? CostPerUnit.Value * Quantity : null;

        /// <summary>
        /// Accumulated Depreciation calculated using straight-line method.
        /// Uses the depreciation rate from the asset's Category.
        /// Formula: (Purchase Price × Category Depreciation Rate × Years Owned)
        /// Capped at Purchase Price and stops depreciating after CalculatedUsefulLifeYears.
        /// Depreciation stops at DisposalDate if asset has been disposed (accounting principle).
        /// </summary>
        [NotMapped]
        public decimal? AccumulatedDepreciation
        {
            get
            {
                if (!PurchasePrice.HasValue || !PurchaseDate.HasValue || Category?.DepreciationRate == null)
                    return null;

                // Use disposal date if asset is disposed, otherwise use current date
                // This ensures depreciation stops at disposal (accounting principle)
                var endDate = DisposalDate.HasValue && DisposalDate.Value < DateTime.UtcNow
                    ? DisposalDate.Value
                    : DateTime.UtcNow;

                var yearsOwned = (endDate - PurchaseDate.Value).TotalDays / 365.25;
                
                // Cap years at calculated useful life (asset is fully depreciated after useful life)
                var usefulLife = CalculatedUsefulLifeYears;
                if (usefulLife.HasValue && yearsOwned > usefulLife.Value)
                {
                    yearsOwned = usefulLife.Value;
                }
                
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

        /// <summary>
        /// Gain or Loss on Disposal = Disposal Value - Net Book Value at Disposal
        /// Positive value = Gain, Negative value = Loss
        /// Only calculated if asset has been disposed (DisposalDate and DisposalValue are set)
        /// </summary>
        [NotMapped]
        public decimal? GainLossOnDisposal
        {
            get
            {
                if (!DisposalDate.HasValue || !DisposalValue.HasValue || !NetBookValue.HasValue)
                    return null;

                return DisposalValue.Value - NetBookValue.Value;
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