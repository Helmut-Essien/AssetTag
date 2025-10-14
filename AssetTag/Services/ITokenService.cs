using AssetTag.Models;

namespace AssetTag.Services
{
    public interface ITokenService
    {
        string CreateAccessToken(ApplicationUser user, IList<string> roles);
        RefreshTokens CreateRefreshToken(string ipAddress);
    }
}
