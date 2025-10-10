using NUlid;

namespace AssetTag.Models
{
    public class Location
    {
        public string LocationId { get; set; } = Ulid.NewUlid().ToString();
        public required string Name { get; set; }
        public string? Description { get; set; }
        public required string Campus { get; set; }
        public string? Building { get; set; }
        public string? Room { get; set; }

        public required ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }
}