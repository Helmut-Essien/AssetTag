using NUlid;
using System.ComponentModel.DataAnnotations;

namespace Shared.Models
{
    public class Location
    {
        public string LocationId { get; set; } = Ulid.NewUlid().ToString();
        public required string Name { get; set; }
        public string? Description { get; set; }
        public required string Campus { get; set; }
        public string? Building { get; set; }
        public string? Room { get; set; }
        [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
        public double? Latitude { get; set; }
        [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
        public double? Longitude { get; set; }

        public required ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }
}