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

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CreateAccessToken(ApplicationUser user, IList<string> roles)
        {
            var jwtsettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtsettings["securitykey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id),

            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                issuer: jwtsettings["Issuer"],
                audience: jwtsettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(double.Parse(jwtsettings["AccessTokenExpirationMinutes"]!)),
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public RefreshTokens CreateRefreshToken(string ipAddress)
        {
            var jwtsettings = _configuration.GetSection("JwtSettings");
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();

            return new RefreshTokens
            {
                Token = Convert.ToBase64String(randomBytes),
                Expires = DateTime.UtcNow.AddDays(double.Parse(jwtsettings["RefreshTokenExpirationDays"]!)),
                Created = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
        }
    }
}
