using NUlid;

namespace Shared.Models
{
    public class Category
    {
        public string CategoryId { get; set; } = Ulid.NewUlid().ToString();
        public required string Name { get; set; }
        public string? Description { get; set; }
        
        /// <summary>
        /// Annual depreciation rate as a percentage (0-100).
        /// Used to calculate depreciation for all assets in this category.
        /// Example: 20.0 = 20% annual depreciation, 12.5 = 12.5% annual depreciation
        /// Supports decimal values for precise depreciation calculations.
        /// </summary>
        public decimal? DepreciationRate { get; set; }

        public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }
}