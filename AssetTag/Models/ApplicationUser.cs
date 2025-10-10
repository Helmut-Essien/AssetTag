using Microsoft.AspNetCore.Identity;

namespace AssetTag.Models
{
    public class ApplicationUser : IdentityUser
    {
        public required string FirstName { get; set; }
        public required string Surname { get; set; }
        public string? OtherNames { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Address { get; set; }
        public string? JobRole { get; set; }
        public string? ProfileImage { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public string? DepartmentId { get; set; }
        public Department? Department { get; set; }
        public ICollection<Asset>? Assets { get; set; } = new List<Asset>();
        public List<RefreshTokens> RefreshTokens { get; set; } = new();
    }
}