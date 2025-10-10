using Microsoft.AspNetCore.Identity;

namespace AssetTag.Models
{
    public class ApplicationUser : IdentityUser
    {
        public List<RefreshTokens> RefreshToken { get; set; } = new();
    }
}
