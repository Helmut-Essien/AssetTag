using NUlid;

namespace Shared.Models
{
    public class Category
    {
        public string CategoryId { get; set; } = Ulid.NewUlid().ToString();
        public required string Name { get; set; }
        public string? Description { get; set; }
        public int? DepreciationRate { get; set; }

        public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }
}