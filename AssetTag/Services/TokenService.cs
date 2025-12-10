//using AssetTag.Models;
//using AssetTag.Services;
//using Microsoft.IdentityModel.Tokens;
//using System.IdentityModel.Tokens.Jwt;
//using System.Security.Claims;
//using System.Security.Cryptography;
//using System.Text;

//public class TokenService : ITokenService
//{
//    private readonly SymmetricSecurityKey _key;
//    private readonly SigningCredentials _creds;
//    private readonly string _issuer;
//    private readonly string _audience;
//    private readonly int _accessTokenExpirationMinutes;
//    private readonly int _refreshTokenExpirationDays;
//    private readonly JwtSecurityTokenHandler _tokenHandler;

//    public TokenService(IConfiguration configuration)
//    {
//        var jwtsettings = configuration.GetSection("JwtSettings");
//        var keyBytes = Encoding.UTF8.GetBytes(jwtsettings["securitykey"]!);

//        _key = new SymmetricSecurityKey(keyBytes);
//        _creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
//        _issuer = jwtsettings["Issuer"]!;
//        _audience = jwtsettings["Audience"]!;
//        _accessTokenExpirationMinutes = int.Parse(jwtsettings["AccessTokenExpirationMinutes"]!);
//        _refreshTokenExpirationDays = int.Parse(jwtsettings["RefreshTokenExpirationDays"]!);
//        _tokenHandler = new JwtSecurityTokenHandler();
//    }

//    public string CreateAccessToken(ApplicationUser user, IList<string> roles)
//    {
//        var claims = new List<Claim>(roles.Count + 6)
//        {
//            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
//            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? ""),
//            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
//            new Claim(ClaimTypes.NameIdentifier, user.Id),
//            new Claim("is_active", user.IsActive ? "true" : "false"),
//            new Claim("security_stamp", user.SecurityStamp ?? "")
//        };

//        // Add roles
//        foreach (var role in roles)
//        {
//            claims.Add(new Claim(ClaimTypes.Role, role));
//        }

//        var token = new JwtSecurityToken(
//            issuer: _issuer,
//            audience: _audience,
//            claims: claims,
//            expires: DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes),
//            signingCredentials: _creds
//        );

//        return _tokenHandler.WriteToken(token);
//    }

//    public RefreshTokens CreateRefreshToken(string ipAddress)
//    {
//        var randomBytes = new byte[48];
//        RandomNumberGenerator.Fill(randomBytes);

//        return new RefreshTokens
//        {
//            Token = Convert.ToBase64String(randomBytes),
//            Expires = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays),
//            Created = DateTime.UtcNow,
//            CreatedByIp = ipAddress
//        };
//    }
//}

using AssetTag.Models;
using AssetTag.Services;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

public class TokenService : ITokenService
{
    private readonly SymmetricSecurityKey _key;
    private readonly SigningCredentials _creds;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenExpirationMinutes;
    private readonly int _refreshTokenExpirationDays;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public TokenService(IConfiguration configuration)
    {
        var jwtsettings = configuration.GetSection("JwtSettings");
        var keyBytes = Encoding.UTF8.GetBytes(jwtsettings["securitykey"]!);

        _key = new SymmetricSecurityKey(keyBytes);
        _creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        _issuer = jwtsettings["Issuer"]!;
        _audience = jwtsettings["Audience"]!;
        _accessTokenExpirationMinutes = int.Parse(jwtsettings["AccessTokenExpirationMinutes"]!);
        _refreshTokenExpirationDays = int.Parse(jwtsettings["RefreshTokenExpirationDays"]!);
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public string CreateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var claims = new List<Claim>(roles.Count + 8)
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id),

            // 🔥 CRITICAL FIX: Add email claims
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),

            new Claim("is_active", user.IsActive ? "true" : "false"),
            new Claim("security_stamp", user.SecurityStamp ?? "")
        };

        //    // CRITICAL: You MUST include Expiration claim
        //    var expiration = DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes);

        //    var claims = new List<Claim>(roles.Count + 10)
        //{
        //    new Claim(JwtRegisteredClaimNames.Sub, user.Id),
        //    new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email ?? "unknown"),
        //    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        //    new Claim(JwtRegisteredClaimNames.Iat,
        //        new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
        //        ClaimValueTypes.Integer64),
        //    new Claim(JwtRegisteredClaimNames.Exp,
        //        new DateTimeOffset(expiration).ToUnixTimeSeconds().ToString(),
        //        ClaimValueTypes.Integer64),
        //    new Claim(ClaimTypes.NameIdentifier, user.Id),
        //    new Claim(ClaimTypes.Email, user.Email),
        //    new Claim(JwtRegisteredClaimNames.Email, user.Email),
        //    new Claim("is_active", user.IsActive ? "true" : "false"),
        //    new Claim("security_stamp", user.SecurityStamp ?? "")
        //};

        // Add roles
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes),
            signingCredentials: _creds
        );
        //     var token = new JwtSecurityToken(
        //    issuer: _issuer,
        //    audience: _audience,
        //    claims: claims,
        //    expires: expiration,  // This must match the Exp claim above
        //    signingCredentials: _creds
        //);

        return _tokenHandler.WriteToken(token);
    }

    public RefreshTokens CreateRefreshToken(string ipAddress)
    {
        var randomBytes = new byte[48];
        RandomNumberGenerator.Fill(randomBytes);

        return new RefreshTokens
        {
            Token = Convert.ToBase64String(randomBytes),
            Expires = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays),
            Created = DateTime.UtcNow,
            CreatedByIp = ipAddress
        };
    }
}