using AssetTag.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AssetTag.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly SymmetricSecurityKey _key;
        private readonly SigningCredentials _creds;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly double _accessTokenExpirationMinutes;
        private readonly double _refreshTokenExpirationDays;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
            var jwtsettings = _configuration.GetSection("JwtSettings");

            // Cache these values to avoid repeated configuration lookups
            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtsettings["securitykey"]!));
            _creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
            _issuer = jwtsettings["Issuer"]!;
            _audience = jwtsettings["Audience"]!;
            _accessTokenExpirationMinutes = double.Parse(jwtsettings["AccessTokenExpirationMinutes"]!);
            _refreshTokenExpirationDays = double.Parse(jwtsettings["RefreshTokenExpirationDays"]!);
        }

        public string CreateAccessToken(ApplicationUser user, IList<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim("is_active", user.IsActive.ToString().ToLower()),
                new Claim("security_stamp", user.SecurityStamp ?? string.Empty)
            };

            // Add roles efficiently
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

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public RefreshTokens CreateRefreshToken(string ipAddress)
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            return new RefreshTokens
            {
                Token = Convert.ToBase64String(randomBytes),
                Expires = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays),
                Created = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
        }
    }
}