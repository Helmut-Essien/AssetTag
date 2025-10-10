namespace AssetTag.Models
{
    public class Location
    {
        public Guid LocationId { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public string Campus { get; set; }
        public string? Building { get; set; }
        public string? Room { get; set; }

        public ICollection<Asset> Assets { get; set; }
    }
}
