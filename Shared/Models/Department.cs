using NUlid;

namespace Shared.Models
{
    public class Department
    {
        public string DepartmentId { get; set; } = Ulid.NewUlid().ToString();
        public required string Name { get; set; }
        public string? Description { get; set; }

        public required ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
        public ICollection<Asset>? Assets { get; set; } = new List<Asset>();
    }
}