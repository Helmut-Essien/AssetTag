using NUlid;

namespace AssetTag.Models
{
    public class Department
    {
        public string DepartmentId { get; set; } = Ulid.NewUlid().ToString();
        public required string Name { get; set; }
        public string? Description { get; set; }

        public required ICollection<ApplicationUser> Users { get; set; }
        public ICollection<Asset>? Assets { get; set; }
    }
}
