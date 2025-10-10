namespace AssetTag.Models
{
    public class Department
    {
        public Guid DepartmentId { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }

        public ICollection<ApplicationUser> Users { get; set; }
        public ICollection<Asset> Assets { get; set; }
    }
}
