

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
//        var keyBytes = Encoding.UTF8.GetBytes(jwtsettings["SecurityKey"]!);

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
//        //var claims = new List<Claim>(roles.Count + 8)
//        //{
//        //    new Claim(JwtRegisteredClaimNames.Sub, user.Id),
//        //    new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email),
//        //    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
//        //    new Claim(ClaimTypes.NameIdentifier, user.Id),

//        //    // 🔥 CRITICAL FIX: Add email claims
//        //    new Claim(ClaimTypes.Email, user.Email),
//        //    new Claim(JwtRegisteredClaimNames.Email, user.Email),

//        //    new Claim("is_active", user.IsActive ? "true" : "false"),
//        //    new Claim("security_stamp", user.SecurityStamp ?? "")
//        //};

//        // CRITICAL: You MUST include Expiration claim
//        var expiration = DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes);

//        var claims = new List<Claim>(roles.Count + 10)
//        {
//            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
//            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email ?? "unknown"),
//            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
//            new Claim(JwtRegisteredClaimNames.Iat,
//                new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
//                ClaimValueTypes.Integer64),
//            new Claim(JwtRegisteredClaimNames.Exp,
//                new DateTimeOffset(expiration).ToUnixTimeSeconds().ToString(),
//                ClaimValueTypes.Integer64),
//            new Claim(ClaimTypes.NameIdentifier, user.Id),
//            new Claim(ClaimTypes.Email, user.Email),
//            new Claim(JwtRegisteredClaimNames.Email, user.Email),
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
//        //     var token = new JwtSecurityToken(
//        //    issuer: _issuer,
//        //    audience: _audience,
//        //    claims: claims,
//        //    expires: expiration,  // This must match the Exp claim above
//        //    signingCredentials: _creds
//        //);

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
    private readonly ILogger<TokenService> _logger;

    public TokenService(IConfiguration configuration, ILogger<TokenService> logger)
    {
        _logger = logger;

        var jwtsettings = configuration.GetSection("JwtSettings");
        var keyBytes = Encoding.UTF8.GetBytes(jwtsettings["SecurityKey"]!);

        _key = new SymmetricSecurityKey(keyBytes);
        _creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        _issuer = jwtsettings["Issuer"]!;
        _audience = jwtsettings["Audience"]!;
        _accessTokenExpirationMinutes = int.Parse(jwtsettings["AccessTokenExpirationMinutes"]!);
        _refreshTokenExpirationDays = int.Parse(jwtsettings["RefreshTokenExpirationDays"]!);
        _tokenHandler = new JwtSecurityTokenHandler();

        // Log configuration
        _logger.LogInformation("=== TOKEN SERVICE CONFIGURATION ===");
        _logger.LogInformation($"Issuer: {_issuer}");
        _logger.LogInformation($"Audience: {_audience}");
        _logger.LogInformation($"AccessTokenExpirationMinutes: {_accessTokenExpirationMinutes}");
        _logger.LogInformation($"RefreshTokenExpirationDays: {_refreshTokenExpirationDays}");
        _logger.LogInformation($"SecurityKey length: {keyBytes.Length} bytes");
        _logger.LogInformation($"Signing Algorithm: {SecurityAlgorithms.HmacSha256}");
        _logger.LogInformation($"API UTC Time at startup: {DateTime.UtcNow:u}");
    }

    public string CreateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var expiration = DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes);

        _logger.LogInformation("=== CREATING ACCESS TOKEN ===");
        _logger.LogInformation($"For user: {user.Email} ({user.Id})");
        _logger.LogInformation($"User SecurityStamp: {user.SecurityStamp}");
        _logger.LogInformation($"User IsActive: {user.IsActive}");
        _logger.LogInformation($"User roles: {string.Join(", ", roles)}");
        _logger.LogInformation($"Expiration calculated: {expiration:u}");
        _logger.LogInformation($"Current API UTC: {DateTime.UtcNow:u}");
        _logger.LogInformation($"Token lifetime: {_accessTokenExpirationMinutes} minutes");

        var claims = new List<Claim>(roles.Count + 10)
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email ?? "unknown"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Exp,
                new DateTimeOffset(expiration).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("is_active", user.IsActive ? "true" : "false"),
            new Claim("security_stamp", user.SecurityStamp ?? "")
        };

        _logger.LogInformation($"Total claims before roles: {claims.Count}");

        // Add roles
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        _logger.LogInformation($"Total claims after roles: {claims.Count}");

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expiration,
            signingCredentials: _creds
        );

        _logger.LogInformation($"JwtSecurityToken created with Expires: {expiration:u}");

        var tokenString = _tokenHandler.WriteToken(token);

        // Verify the written token can be read back
        try
        {
            var decodedToken = _tokenHandler.ReadJwtToken(tokenString);
            _logger.LogInformation($"=== TOKEN CREATION VERIFICATION ===");
            _logger.LogInformation($"Token can be decoded successfully");
            _logger.LogInformation($"Decoded ValidTo: {decodedToken.ValidTo:u}");
            _logger.LogInformation($"Decoded ValidFrom: {decodedToken.ValidFrom:u}");
            _logger.LogInformation($"Decoded Issuer: {decodedToken.Issuer}");
            _logger.LogInformation($"Decoded Audiences: {string.Join(", ", decodedToken.Audiences)}");
            _logger.LogInformation($"Exp claim exists: {decodedToken.Claims.Any(c => c.Type == "exp")}");

            var expClaim = decodedToken.Claims.FirstOrDefault(c => c.Type == "exp");
            if (expClaim != null)
            {
                if (long.TryParse(expClaim.Value, out var expUnix))
                {
                    var expTime = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                    _logger.LogInformation($"Exp claim value: {expUnix} = {expTime:u}");
                    _logger.LogInformation($"Exp claim matches ValidTo: {expTime == decodedToken.ValidTo}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify created token");
        }

        _logger.LogInformation($"Access token created, length: {tokenString.Length} chars");
        _logger.LogInformation($"=== ACCESS TOKEN CREATION COMPLETE ===");

        return tokenString;
    }

    public RefreshTokens CreateRefreshToken(string ipAddress)
    {
        var randomBytes = new byte[48];
        RandomNumberGenerator.Fill(randomBytes);

        var token = Convert.ToBase64String(randomBytes);
        var expires = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);

        _logger.LogDebug($"Creating refresh token for IP: {ipAddress}");
        _logger.LogDebug($"Refresh token expires: {expires:u}");
        _logger.LogDebug($"Refresh token length: {token.Length} chars");

        return new RefreshTokens
        {
            Token = token,
            Expires = expires,
            Created = DateTime.UtcNow,
            CreatedByIp = ipAddress
        };
    }
}