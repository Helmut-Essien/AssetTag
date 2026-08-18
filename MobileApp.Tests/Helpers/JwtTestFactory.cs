using System.IdentityModel.Tokens.Jwt;

namespace MobileApp.Tests.Helpers;

public static class JwtTestFactory
{
    public static string CreateToken(DateTime? expires = null, DateTime? notBefore = null)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            notBefore: notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            expires: expires ?? DateTime.UtcNow.AddHours(1));
        return handler.WriteToken(token);
    }

    public static string CreateExpiredToken() =>
        CreateToken(expires: DateTime.UtcNow.AddMinutes(-10), notBefore: DateTime.UtcNow.AddHours(-2));

    public static string CreateFreshToken() =>
        CreateToken(expires: DateTime.UtcNow.AddHours(1), notBefore: DateTime.UtcNow.AddSeconds(-2));
}
