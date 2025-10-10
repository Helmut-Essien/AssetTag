namespace AssetTag.Models
{
    public class Category
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }

        public ICollection<Asset> Assets { get; set; }
    }
}
