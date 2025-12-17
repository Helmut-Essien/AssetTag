//using AssetTag.Data;
//using AssetTag.Models;
//using AssetTag.Services;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using Shared.DTOs;

//namespace AssetTag.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class AuthController(
//        UserManager<ApplicationUser> userManager,
//        ApplicationDbContext context,
//        ITokenService tokenService,
//        IEmailService emailService,
//        IConfiguration configuration,
//        ILogger<AuthController> logger) : ControllerBase
//    {
//        private readonly UserManager<ApplicationUser> _userManager = userManager;
//        private readonly ApplicationDbContext _context = context;
//        private readonly ITokenService _tokenService = tokenService;
//        private readonly IEmailService _emailService = emailService;
//        private readonly IConfiguration _configuration = configuration;
//        private readonly ILogger<AuthController> _logger = logger;

//        // Static password verifier for performance
//        private static readonly PasswordHasher<ApplicationUser> _passwordHasher = new();

//        [HttpPost("register")]
//        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
//        {
//            var user = new ApplicationUser
//            {
//                UserName = dto.Username,
//                Email = dto.Email,
//                FirstName = dto.FirstName,
//                Surname = dto.Surname
//            };

//            var result = await _userManager.CreateAsync(user, dto.Password);
//            return !result.Succeeded
//                ? BadRequest(result.Errors)
//                : Ok("User registered successfully.");
//        }

//        [HttpPost("login")]
//        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
//        {
//            try
//            {
//                // Single optimized query with projection
//                var userData = await _userManager.Users
//                    .AsNoTracking()
//                    .Where(u => u.Email == dto.Email)
//                    .Select(u => new { u.Id, u.PasswordHash, u.IsActive, u.UserName, u.Email })
//                    .FirstOrDefaultAsync();

//                if (userData is null)
//                    return Unauthorized(new { Message = "Invalid email or password." });

//                // Check deactivation status
//                if (!userData.IsActive)
//                {
//                    return Unauthorized(new
//                    {
//                        Message = "Account deactivated. Contact administrator.",
//                        Code = "ACCOUNT_DEACTIVATED",
//                        IsDeactivated = true
//                    });
//                }

//                // Password verification
//                if (!VerifyPassword(userData.PasswordHash, dto.Password))
//                    return Unauthorized(new { Message = "Invalid email or password." });

//                // Get user for roles and token operations
//                var user = await _userManager.FindByIdAsync(userData.Id);
//                if (user is null)
//                    return Unauthorized(new { Message = "User not found." });

//                // Parallel operations for maximum performance
//                var rolesTask = _userManager.GetRolesAsync(user);
//                var refreshTokenTask = Task.Run(() => _tokenService.CreateRefreshToken(GetIpAddress()));
//                await Task.WhenAll(rolesTask, refreshTokenTask);

//                var roles = await rolesTask;
//                var refreshToken = await refreshTokenTask;

//                // Add refresh token and update
//                user.RefreshTokens.Add(refreshToken);
//                await _userManager.UpdateAsync(user);

//                // Create access token
//                var accessToken = _tokenService.CreateAccessToken(user, roles);

//                return Ok(new TokenResponseDTO(accessToken, refreshToken.Token));
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Login error for {Email}", dto.Email);
//                return StatusCode(500, new { Message = "An error occurred during login." });
//            }
//        }

//        //[HttpPost("refresh-token")]
//        //public async Task<IActionResult> RefreshToken([FromBody] TokenResponseDTO dto)
//        //{
//        //    try
//        //    {
//        //        // Fast validation query first
//        //        var isValidToken = await _context.RefreshTokens
//        //            .AsNoTracking()
//        //            .AnyAsync(rt => rt.Token == dto.RefreshToken &&
//        //                           rt.Revoked == null &&
//        //                           rt.Expires > DateTime.UtcNow);

//        //        if (!isValidToken)
//        //            return Unauthorized(new { Message = "Invalid or expired refresh token." });

//        //        // Get full token with user for update
//        //        var refreshToken = await _context.RefreshTokens
//        //            .Include(rt => rt.ApplicationUser)
//        //            .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken);

//        //        if (refreshToken?.ApplicationUser is null)
//        //            return Unauthorized(new { Message = "Token or user not found." });

//        //        var user = refreshToken.ApplicationUser;

//        //        // Parallel operations for maximum performance
//        //        var rolesTask = _userManager.GetRolesAsync(user);
//        //        var newRefreshTokenTask = Task.Run(() => _tokenService.CreateRefreshToken(GetIpAddress()));
//        //        await Task.WhenAll(rolesTask, newRefreshTokenTask);

//        //        var roles = await rolesTask;
//        //        var newRefreshToken = await newRefreshTokenTask;

//        //        // Update operations
//        //        refreshToken.Revoked = DateTime.UtcNow;
//        //        refreshToken.RevokedByIp = GetIpAddress();
//        //        refreshToken.ReplacedByToken = newRefreshToken.Token;

//        //        // Add new token directly to context for better performance
//        //        newRefreshToken.ApplicationUserId = user.Id;
//        //        _context.RefreshTokens.Add(newRefreshToken);

//        //        // Single save operation for both updates
//        //        await _context.SaveChangesAsync();

//        //        // Create access token
//        //        var newAccessToken = _tokenService.CreateAccessToken(user, roles);

//        //        return Ok(new TokenResponseDTO(newAccessToken, newRefreshToken.Token));
//        //    }
//        //    catch (Exception ex)
//        //    {
//        //        _logger.LogError(ex, "Error refreshing token");
//        //        return StatusCode(500, new { Message = "An error occurred while refreshing token." });
//        //    }
//        //}



//        // Add this improved version of the refresh-token endpoint to your AuthController

//        [HttpPost("refresh-token")]
//        public async Task<IActionResult> RefreshToken([FromBody] TokenResponseDTO dto)
//        {
//            try
//            {
//                _logger.LogInformation("Refresh token request received");

//                if (string.IsNullOrWhiteSpace(dto.RefreshToken))
//                {
//                    _logger.LogWarning("Refresh token request with null/empty token");
//                    return Unauthorized(new { Message = "Refresh token is required." });
//                }

//                // Log token length for debugging (not the actual token)
//                _logger.LogDebug("Refresh token length: {Length}", dto.RefreshToken.Length);

//                // Fast validation query first
//                var isValidToken = await _context.RefreshTokens
//                    .AsNoTracking()
//                    .AnyAsync(rt => rt.Token == dto.RefreshToken &&
//                                   rt.Revoked == null &&
//                                   rt.Expires > DateTime.UtcNow);

//                if (!isValidToken)
//                {
//                    _logger.LogWarning("Invalid or expired refresh token");

//                    // Check if token exists at all
//                    var tokenExists = await _context.RefreshTokens
//                        .AsNoTracking()
//                        .AnyAsync(rt => rt.Token == dto.RefreshToken);

//                    if (!tokenExists)
//                    {
//                        _logger.LogWarning("Refresh token not found in database");
//                    }
//                    else
//                    {
//                        // Token exists but is invalid - check why
//                        var tokenInfo = await _context.RefreshTokens
//                            .AsNoTracking()
//                            .Where(rt => rt.Token == dto.RefreshToken)
//                            .Select(rt => new { rt.Revoked, rt.Expires })
//                            .FirstOrDefaultAsync();

//                        if (tokenInfo?.Revoked != null)
//                        {
//                            _logger.LogWarning("Refresh token was revoked at {RevokedAt}", tokenInfo.Revoked);
//                        }
//                        else if (tokenInfo?.Expires < DateTime.UtcNow)
//                        {
//                            _logger.LogWarning("Refresh token expired at {ExpiresAt}", tokenInfo.Expires);
//                        }
//                    }

//                    return Unauthorized(new { Message = "Invalid or expired refresh token." });
//                }

//                // Get full token with user for update
//                var refreshToken = await _context.RefreshTokens
//                    .Include(rt => rt.ApplicationUser)
//                    .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken);

//                if (refreshToken?.ApplicationUser is null)
//                {
//                    _logger.LogError("Refresh token found but user is null");
//                    return Unauthorized(new { Message = "Token or user not found." });
//                }

//                var user = refreshToken.ApplicationUser;

//                // Check if user is still active
//                if (!user.IsActive)
//                {
//                    _logger.LogWarning("Refresh attempt for inactive user {UserId}", user.Id);
//                    return Unauthorized(new { Message = "User account is deactivated." });
//                }

//                _logger.LogInformation("Valid refresh token for user {UserId}", user.Id);

//                // Parallel operations for maximum performance
//                var rolesTask = _userManager.GetRolesAsync(user);
//                var newRefreshTokenTask = Task.Run(() => _tokenService.CreateRefreshToken(GetIpAddress()));
//                await Task.WhenAll(rolesTask, newRefreshTokenTask);

//                var roles = await rolesTask;
//                var newRefreshToken = await newRefreshTokenTask;

//                // Update operations
//                refreshToken.Revoked = DateTime.UtcNow;
//                refreshToken.RevokedByIp = GetIpAddress();
//                refreshToken.ReplacedByToken = newRefreshToken.Token;

//                // Add new token directly to context for better performance
//                newRefreshToken.ApplicationUserId = user.Id;
//                _context.RefreshTokens.Add(newRefreshToken);

//                // Single save operation for both updates
//                await _context.SaveChangesAsync();

//                // Create access token
//                var newAccessToken = _tokenService.CreateAccessToken(user, roles);

//                _logger.LogInformation("Successfully refreshed tokens for user {UserId}", user.Id);

//                return Ok(new TokenResponseDTO(newAccessToken, newRefreshToken.Token));
//            }
//            catch (DbUpdateException ex)
//            {
//                _logger.LogError(ex, "Database error during token refresh");
//                return StatusCode(500, new { Message = "A database error occurred." });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Unexpected error during token refresh");
//                return StatusCode(500, new { Message = "An error occurred while refreshing token." });
//            }
//        }





//        [HttpPost("revoke")]
//        public async Task<IActionResult> Revoke([FromBody] TokenResponseDTO dto)
//        {
//            try
//            {
//                // Optimized: Direct SQL execution for maximum performance
//                var affectedRows = await _context.Database.ExecuteSqlRawAsync(
//                    @"UPDATE RefreshTokens 
//                      SET Revoked = {0}, RevokedByIp = {1} 
//                      WHERE Token = {2} AND Revoked IS NULL",
//                    DateTime.UtcNow, GetIpAddress(), dto.RefreshToken);

//                return affectedRows > 0
//                    ? Ok(new { Message = "Token revoked successfully." })
//                    : BadRequest(new { Message = "Token not found or already revoked." });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error revoking token");
//                return StatusCode(500, new { Message = "An error occurred while revoking token." });
//            }
//        }

//        [HttpPost("forgot-password")]
//        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO dto)
//        {
//            try
//            {
//                // Single query with only needed fields
//                var user = await _userManager.Users
//                    .AsNoTracking()
//                    .Where(u => u.Email == dto.Email)
//                    .Select(u => new { u.Id, u.Email })
//                    .FirstOrDefaultAsync();

//                if (user is null)
//                {
//                    // Return same message for security - don't reveal user existence
//                    return Ok(new { Message = "If the email exists, a password reset link has been sent." });
//                }

//                // Generate token
//                var fullUser = await _userManager.FindByIdAsync(user.Id);
//                var token = await _userManager.GeneratePasswordResetTokenAsync(fullUser);

//                // Prepare email data
//                var frontendBaseUrl = _configuration["FrontendBaseUrl"] ?? "https://1qtrdwgx-44369.uks1.devtunnels.ms";
//                var resetUrl = $"{frontendBaseUrl}/Account/ResetPassword";

//                // Fire and forget email for performance
//                _ = Task.Run(async () =>
//                {
//                    try
//                    {
//                        await _emailService.SendPasswordResetEmailAsync(user.Email, token, resetUrl);
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger.LogWarning(ex, "Failed to send password reset email to {Email}", user.Email);
//                    }
//                });

//                return Ok(new { Message = "If the email exists, a password reset link has been sent." });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error in forgot password for {Email}", dto.Email);
//                return StatusCode(500, new { Message = "An error occurred." });
//            }
//        }

//        [HttpPost("reset-password")]
//        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO dto)
//        {
//            try
//            {
//                // Find user with minimal data
//                var user = await _userManager.FindByEmailAsync(dto.Email);
//                if (user is null)
//                {
//                    // Don't reveal user existence
//                    return BadRequest(new { Message = "Invalid reset token." });
//                }

//                // Reset password
//                var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
//                if (!result.Succeeded)
//                {
//                    return BadRequest(new
//                    {
//                        Message = "Failed to reset password.",
//                        Errors = result.Errors.Select(e => e.Description)
//                    });
//                }

//                // Revoke all active tokens in background for security
//                _ = Task.Run(async () =>
//                {
//                    try
//                    {
//                        await _context.Database.ExecuteSqlRawAsync(
//                            @"UPDATE RefreshTokens 
//                              SET Revoked = {0}, RevokedByIp = {1} 
//                              WHERE ApplicationUserId = {2} AND Revoked IS NULL",
//                            DateTime.UtcNow, GetIpAddress(), user.Id);
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger.LogWarning(ex, "Failed to revoke tokens for user {UserId}", user.Id);
//                    }
//                });

//                return Ok(new { Message = "Password has been reset successfully." });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error resetting password for {Email}", dto.Email);
//                return StatusCode(500, new { Message = "An error occurred while resetting password." });
//            }
//        }

//        [HttpPost("validate-invitation")]
//        [AllowAnonymous]
//        public async Task<ActionResult> ValidateInvitation([FromBody] ValidateInvitationDTO dto)
//        {
//            try
//            {
//                var invitation = await _context.Invitations
//                    .AsNoTracking()
//                    .FirstOrDefaultAsync(i => i.Token == dto.Token && !i.IsUsed);

//                if (invitation is null)
//                    return BadRequest(new { Message = "Invalid or expired invitation token." });

//                if (invitation.ExpiresAt < DateTime.UtcNow)
//                    return BadRequest(new { Message = "This invitation has expired." });

//                return Ok(new
//                {
//                    Message = "Invitation is valid.",
//                    Email = invitation.Email,
//                    Role = invitation.Role
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error validating invitation token");
//                return StatusCode(500, new { Message = "An internal error occurred." });
//            }
//        }

//        [HttpPost("register-with-invitation")]
//        [AllowAnonymous]
//        public async Task<IActionResult> RegisterWithInvitation([FromBody] RegisterWithInvitationDTO dto)
//        {
//            try
//            {
//                _logger.LogInformation("Starting registration with invitation for email: {Email}", dto.Email);

//                // Validate invitation
//                var invitation = await _context.Invitations
//                    .FirstOrDefaultAsync(i => i.Token == dto.Token && !i.IsUsed);

//                if (invitation is null)
//                {
//                    _logger.LogWarning("Invalid or used invitation token: {Token}", dto.Token);
//                    return BadRequest(new { Message = "Invalid or expired invitation token." });
//                }

//                if (invitation.ExpiresAt < DateTime.UtcNow)
//                {
//                    _logger.LogWarning("Expired invitation token: {Token}, expired at: {ExpiresAt}",
//                        dto.Token, invitation.ExpiresAt);
//                    return BadRequest(new { Message = "This invitation has expired." });
//                }

//                // Check if email matches
//                if (!string.Equals(invitation.Email, dto.Email, StringComparison.OrdinalIgnoreCase))
//                {
//                    _logger.LogWarning("Email mismatch for invitation. Expected: {Expected}, Got: {Actual}",
//                        invitation.Email, dto.Email);
//                    return BadRequest(new { Message = "Email does not match the invitation." });
//                }

//                // Check if user already exists
//                if (await _userManager.FindByEmailAsync(dto.Email) is not null)
//                {
//                    _logger.LogWarning("User already exists with email: {Email}", dto.Email);
//                    return BadRequest(new { Message = "A user with this email already exists." });
//                }

//                // Check if username is already taken
//                if (await _userManager.FindByNameAsync(dto.Username) is not null)
//                {
//                    _logger.LogWarning("Username already taken: {Username}", dto.Username);
//                    return BadRequest(new { Message = "Username is already taken. Please choose a different username." });
//                }

//                // Create user
//                var user = new ApplicationUser
//                {
//                    UserName = dto.Username,
//                    Email = dto.Email,
//                    FirstName = dto.FirstName,
//                    Surname = dto.Surname,
//                    OtherNames = dto.OtherNames,
//                    DateOfBirth = dto.DateOfBirth,
//                    Address = dto.Address,
//                    JobRole = dto.JobRole,
//                    DepartmentId = dto.DepartmentId,
//                    IsActive = true,
//                    DateCreated = DateTime.UtcNow
//                };

//                _logger.LogInformation("Creating user: {Username}, {Email}", dto.Username, dto.Email);

//                var result = await _userManager.CreateAsync(user, dto.Password);
//                if (!result.Succeeded)
//                {
//                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
//                    _logger.LogError("Failed to create user {Email}. Errors: {Errors}", dto.Email, errors);

//                    return BadRequest(new
//                    {
//                        Message = "Failed to create user account.",
//                        Errors = result.Errors.Select(e => e.Description)
//                    });
//                }

//                // Assign role from invitation
//                if (!string.IsNullOrEmpty(invitation.Role))
//                {
//                    _logger.LogInformation("Assigning role {Role} to user {Email}", invitation.Role, dto.Email);
//                    var roleResult = await _userManager.AddToRoleAsync(user, invitation.Role);
//                    if (!roleResult.Succeeded)
//                    {
//                        var roleErrors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
//                        _logger.LogWarning("Failed to assign role {Role} to user {Email}. Errors: {Errors}",
//                            invitation.Role, dto.Email, roleErrors);
//                        // Continue anyway - user is created but role assignment failed
//                    }
//                }

//                // Mark invitation as used
//                invitation.IsUsed = true;
//                invitation.UsedAt = DateTime.UtcNow;
//                await _context.SaveChangesAsync();

//                _logger.LogInformation("Successfully registered user with invitation: {Email}", dto.Email);

//                return Ok(new { Message = "User registered successfully. You can now login." });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error registering user with invitation for {Email}", dto.Email);
//                return StatusCode(500, new { Message = "An internal error occurred during registration." });
//            }
//        }

//        private static bool VerifyPassword(string? passwordHash, string password)
//        {
//            return passwordHash is not null &&
//                   _passwordHasher.VerifyHashedPassword(null, passwordHash, password)
//                   != PasswordVerificationResult.Failed;
//        }

//        private string GetIpAddress()
//        {
//            if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
//                return forwardedFor.ToString();

//            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
//        }
//    }
//}





using AssetTag.Data;
using AssetTag.Models;
using AssetTag.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AssetTag.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ITokenService tokenService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<AuthController> logger) : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly ApplicationDbContext _context = context;
        private readonly ITokenService _tokenService = tokenService;
        private readonly IEmailService _emailService = emailService;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<AuthController> _logger = logger;

        // Static password verifier for performance
        private static readonly PasswordHasher<ApplicationUser> _passwordHasher = new();

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            try
            {
                _logger.LogInformation("=== LOGIN START ===");
                _logger.LogInformation($"Login attempt for email: {dto.Email}");
                _logger.LogInformation($"Request from IP: {GetIpAddress()}");
                _logger.LogInformation($"API UTC Time: {DateTime.UtcNow:u}");

                // Single optimized query with projection
                var userData = await _userManager.Users
                    .AsNoTracking()
                    .Where(u => u.Email == dto.Email)
                    .Select(u => new { u.Id, u.PasswordHash, u.IsActive, u.UserName, u.Email, u.SecurityStamp })
                    .FirstOrDefaultAsync();

                if (userData is null)
                {
                    _logger.LogWarning($"User not found for email: {dto.Email}");
                    _logger.LogInformation("=== LOGIN END (User not found) ===");
                    return Unauthorized(new { Message = "Invalid email or password." });
                }

                _logger.LogInformation($"User found: {userData.Email}, ID: {userData.Id}, IsActive: {userData.IsActive}");

                // Check deactivation status
                if (!userData.IsActive)
                {
                    _logger.LogWarning($"Account deactivated for: {dto.Email}");
                    _logger.LogInformation("=== LOGIN END (Account deactivated) ===");
                    return Unauthorized(new
                    {
                        Message = "Account deactivated. Contact administrator.",
                        Code = "ACCOUNT_DEACTIVATED",
                        IsDeactivated = true
                    });
                }

                // Password verification
                if (!VerifyPassword(userData.PasswordHash, dto.Password))
                {
                    _logger.LogWarning($"Invalid password for: {dto.Email}");
                    _logger.LogInformation("=== LOGIN END (Invalid password) ===");
                    return Unauthorized(new { Message = "Invalid email or password." });
                }

                // Get user for roles and token operations
                var user = await _userManager.FindByIdAsync(userData.Id);
                if (user is null)
                {
                    _logger.LogError($"User retrieval failed for ID: {userData.Id}");
                    _logger.LogInformation("=== LOGIN END (User retrieval failed) ===");
                    return Unauthorized(new { Message = "User not found." });
                }

                _logger.LogInformation($"User retrieved successfully: {user.UserName}, SecurityStamp: {user.SecurityStamp}");

                // Parallel operations for maximum performance
                var rolesTask = _userManager.GetRolesAsync(user);
                var refreshTokenTask = Task.Run(() => _tokenService.CreateRefreshToken(GetIpAddress()));
                await Task.WhenAll(rolesTask, refreshTokenTask);

                var roles = await rolesTask;
                var refreshToken = await refreshTokenTask;

                _logger.LogInformation($"User roles: {string.Join(", ", roles)}");
                _logger.LogInformation($"Refresh token created - Expires: {refreshToken.Expires:u}");

                // Add refresh token and update
                user.RefreshTokens.Add(refreshToken);
                await _userManager.UpdateAsync(user);

                // Log security stamp after update
                await _userManager.UpdateSecurityStampAsync(user);
                _logger.LogInformation($"Security stamp updated: {user.SecurityStamp}");

                // Create access token
                var accessToken = _tokenService.CreateAccessToken(user, roles);

                // Decode and log token details
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(accessToken);

                    _logger.LogInformation($"=== TOKEN CREATION DETAILS ===");
                    _logger.LogInformation($"Access token created successfully");
                    _logger.LogInformation($"Token ValidFrom: {jwtToken.ValidFrom:u}");
                    _logger.LogInformation($"Token ValidTo: {jwtToken.ValidTo:u}");
                    _logger.LogInformation($"Token Issuer: {jwtToken.Issuer}");
                    _logger.LogInformation($"Token Audience: {string.Join(", ", jwtToken.Audiences)}");
                    _logger.LogInformation($"Token contains exp claim: {jwtToken.Claims.Any(c => c.Type == "exp")}");
                    _logger.LogInformation($"Token contains nbf claim: {jwtToken.Claims.Any(c => c.Type == "nbf")}");
                    _logger.LogInformation($"Token contains iat claim: {jwtToken.Claims.Any(c => c.Type == "iat")}");
                    _logger.LogInformation($"User ID in token: {jwtToken.Subject}");
                    _logger.LogInformation($"Roles in token: {string.Join(", ", jwtToken.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value))}");

                    // Check exp claim value
                    var expClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "exp");
                    if (expClaim != null)
                    {
                        if (long.TryParse(expClaim.Value, out var expUnix))
                        {
                            var expTime = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                            _logger.LogInformation($"Exp claim value: {expUnix} = {expTime:u}");
                            _logger.LogInformation($"Exp claim matches ValidTo: {expTime == jwtToken.ValidTo}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decode created token for logging");
                }

                _logger.LogInformation($"=== LOGIN SUCCESS ===");
                _logger.LogInformation($"Login successful for: {dto.Email}");
                _logger.LogInformation($"Access token length: {accessToken.Length}");
                _logger.LogInformation($"Refresh token length: {refreshToken.Token.Length}");
                _logger.LogInformation($"API UTC Time at completion: {DateTime.UtcNow:u}");
                _logger.LogInformation("=== LOGIN END ===");

                return Ok(new TokenResponseDTO(accessToken, refreshToken.Token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Login error for {dto.Email}");
                _logger.LogInformation("=== LOGIN END (Exception) ===");
                return StatusCode(500, new { Message = "An error occurred during login." });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenResponseDTO dto)
        {
            _logger.LogInformation("=== REFRESH TOKEN START ===");
            _logger.LogInformation($"API UTC Time: {DateTime.UtcNow:u}");
            _logger.LogInformation($"Refresh token length: {dto.RefreshToken?.Length ?? 0}");
            _logger.LogInformation($"Access token length: {dto.AccessToken?.Length ?? 0}");

            try
            {
                if (string.IsNullOrWhiteSpace(dto.RefreshToken))
                {
                    _logger.LogWarning("Refresh token request with null/empty token");
                    _logger.LogInformation("=== REFRESH TOKEN END (Empty token) ===");
                    return Unauthorized(new { Message = "Refresh token is required." });
                }

                // Also validate the access token if provided
                if (!string.IsNullOrWhiteSpace(dto.AccessToken))
                {
                    try
                    {
                        var handler = new JwtSecurityTokenHandler();
                        if (handler.CanReadToken(dto.AccessToken))
                        {
                            var oldToken = handler.ReadJwtToken(dto.AccessToken);
                            _logger.LogInformation($"=== OLD ACCESS TOKEN ANALYSIS ===");
                            _logger.LogInformation($"Old token ValidTo: {oldToken.ValidTo:u}");
                            _logger.LogInformation($"Old token Issuer: {oldToken.Issuer}");
                            _logger.LogInformation($"Old token Audience: {string.Join(", ", oldToken.Audiences)}");
                            _logger.LogInformation($"Old token age: {DateTime.UtcNow - oldToken.ValidFrom}");
                            _logger.LogInformation($"Old token expires in: {oldToken.ValidTo - DateTime.UtcNow}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to decode old access token");
                    }
                }

                // Fast validation query first
                var isValidToken = await _context.RefreshTokens
                    .AsNoTracking()
                    .AnyAsync(rt => rt.Token == dto.RefreshToken &&
                                   rt.Revoked == null &&
                                   rt.Expires > DateTime.UtcNow);

                _logger.LogInformation($"Refresh token exists and valid in DB: {isValidToken}");

                if (!isValidToken)
                {
                    _logger.LogWarning("Invalid or expired refresh token");

                    // Check if token exists at all
                    var tokenExists = await _context.RefreshTokens
                        .AsNoTracking()
                        .AnyAsync(rt => rt.Token == dto.RefreshToken);

                    _logger.LogInformation($"Refresh token exists in DB (any status): {tokenExists}");

                    if (!tokenExists)
                    {
                        _logger.LogWarning("Refresh token not found in database");
                        _logger.LogInformation("=== REFRESH TOKEN END (Token not found) ===");
                        return Unauthorized(new { Message = "Invalid or expired refresh token." });
                    }
                    else
                    {
                        // Token exists but is invalid - check why
                        var tokenInfo = await _context.RefreshTokens
                            .AsNoTracking()
                            .Where(rt => rt.Token == dto.RefreshToken)
                            .Select(rt => new { rt.Revoked, rt.Expires, rt.ApplicationUserId })
                            .FirstOrDefaultAsync();

                        if (tokenInfo?.Revoked != null)
                        {
                            _logger.LogWarning($"Refresh token was revoked at {tokenInfo.Revoked}");
                        }
                        else if (tokenInfo?.Expires < DateTime.UtcNow)
                        {
                            _logger.LogWarning($"Refresh token expired at {tokenInfo.Expires}, Current UTC: {DateTime.UtcNow}");
                            _logger.LogWarning($"Token expired {DateTime.UtcNow - tokenInfo.Expires} ago");
                        }
                        else if (tokenInfo?.Expires > DateTime.UtcNow)
                        {
                            _logger.LogWarning($"Refresh token exists and not expired, but still invalid. Expires at {tokenInfo.Expires}");
                        }
                    }

                    _logger.LogInformation("=== REFRESH TOKEN END (Invalid token) ===");
                    return Unauthorized(new { Message = "Invalid or expired refresh token." });
                }

                // Get full token with user for update
                var refreshToken = await _context.RefreshTokens
                    .Include(rt => rt.ApplicationUser)
                    .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken);

                if (refreshToken?.ApplicationUser is null)
                {
                    _logger.LogError("Refresh token found but user is null");
                    _logger.LogInformation("=== REFRESH TOKEN END (User null) ===");
                    return Unauthorized(new { Message = "Token or user not found." });
                }

                var user = refreshToken.ApplicationUser;

                _logger.LogInformation($"User found for refresh: {user.Email}, ID: {user.Id}, IsActive: {user.IsActive}");
                _logger.LogInformation($"Refresh token details - Created: {refreshToken.Created:u}, Expires: {refreshToken.Expires:u}");
                _logger.LogInformation($"Refresh token age: {DateTime.UtcNow - refreshToken.Created}");
                _logger.LogInformation($"Refresh token expires in: {refreshToken.Expires - DateTime.UtcNow}");

                // Check if user is still active
                if (!user.IsActive)
                {
                    _logger.LogWarning($"Refresh attempt for inactive user {user.Id}");
                    _logger.LogInformation("=== REFRESH TOKEN END (User inactive) ===");
                    return Unauthorized(new { Message = "User account is deactivated." });
                }

                _logger.LogInformation($"Valid refresh token for user {user.Id}");

                // Parallel operations for maximum performance
                var rolesTask = _userManager.GetRolesAsync(user);
                var newRefreshTokenTask = Task.Run(() => _tokenService.CreateRefreshToken(GetIpAddress()));
                await Task.WhenAll(rolesTask, newRefreshTokenTask);

                var roles = await rolesTask;
                var newRefreshToken = await newRefreshTokenTask;

                _logger.LogInformation($"User roles: {string.Join(", ", roles)}");
                _logger.LogInformation($"New refresh token created - Expires: {newRefreshToken.Expires:u}");

                // Update operations
                refreshToken.Revoked = DateTime.UtcNow;
                refreshToken.RevokedByIp = GetIpAddress();
                refreshToken.ReplacedByToken = newRefreshToken.Token;

                _logger.LogInformation($"Old refresh token revoked at: {refreshToken.Revoked:u}");

                // Add new token directly to context for better performance
                newRefreshToken.ApplicationUserId = user.Id;
                _context.RefreshTokens.Add(newRefreshToken);

                // Single save operation for both updates
                await _context.SaveChangesAsync();

                // Create access token
                var newAccessToken = _tokenService.CreateAccessToken(user, roles);

                // Decode and log new token details
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(newAccessToken);

                    _logger.LogInformation($"=== NEW ACCESS TOKEN DETAILS ===");
                    _logger.LogInformation($"New access token created");
                    _logger.LogInformation($"New token ValidFrom: {jwtToken.ValidFrom:u}");
                    _logger.LogInformation($"New token ValidTo: {jwtToken.ValidTo:u}");
                    _logger.LogInformation($"New token Issuer: {jwtToken.Issuer}");
                    _logger.LogInformation($"New token Audience: {string.Join(", ", jwtToken.Audiences)}");
                    _logger.LogInformation($"API UTC Time during token creation: {DateTime.UtcNow:u}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decode new access token");
                }

                _logger.LogInformation($"Successfully refreshed tokens for user {user.Id}");
                _logger.LogInformation($"New access token length: {newAccessToken.Length}");
                _logger.LogInformation($"New refresh token length: {newRefreshToken.Token.Length}");
                _logger.LogInformation("=== REFRESH TOKEN END (Success) ===");

                return Ok(new TokenResponseDTO(newAccessToken, newRefreshToken.Token));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error during token refresh");
                _logger.LogInformation("=== REFRESH TOKEN END (Database error) ===");
                return StatusCode(500, new { Message = "A database error occurred." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token refresh");
                _logger.LogInformation("=== REFRESH TOKEN END (Unexpected error) ===");
                return StatusCode(500, new { Message = "An error occurred while refreshing token." });
            }
        }

        // Also add a diagnostic endpoint to validate tokens manually
        [HttpPost("validate-token-diagnostic")]
        [AllowAnonymous]
        public IActionResult ValidateTokenDiagnostic([FromBody] TokenValidationRequest request)
        {
            try
            {
                _logger.LogInformation("=== TOKEN VALIDATION DIAGNOSTIC ===");
                _logger.LogInformation($"API UTC Time: {DateTime.UtcNow:u}");
                _logger.LogInformation($"Token length: {request.Token?.Length ?? 0}");

                if (string.IsNullOrWhiteSpace(request.Token))
                {
                    return BadRequest(new { Message = "Token is required" });
                }

                var handler = new JwtSecurityTokenHandler();

                if (!handler.CanReadToken(request.Token))
                {
                    _logger.LogWarning("Token cannot be read by JwtSecurityTokenHandler");
                    return BadRequest(new { Message = "Token cannot be read" });
                }

                // Read token without validation first
                var jwtToken = handler.ReadJwtToken(request.Token);

                _logger.LogInformation($"=== TOKEN DECODED (NO VALIDATION) ===");
                _logger.LogInformation($"Token Issuer: {jwtToken.Issuer}");
                _logger.LogInformation($"Token Audiences: {string.Join(", ", jwtToken.Audiences)}");
                _logger.LogInformation($"Token ValidFrom: {jwtToken.ValidFrom:u}");
                _logger.LogInformation($"Token ValidTo: {jwtToken.ValidTo:u}");
                _logger.LogInformation($"Token Algorithm: {jwtToken.Header.Alg}");
                _logger.LogInformation($"Token Type: {jwtToken.Header.Typ}");
                _logger.LogInformation($"Token Subject: {jwtToken.Subject}");
                _logger.LogInformation($"Token contains {jwtToken.Claims.Count()} claims");

                // Show all claims
                foreach (var claim in jwtToken.Claims)
                {
                    _logger.LogInformation($"Claim: {claim.Type} = {claim.Value}");
                }

                // Now try to validate with your API's configuration
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var key = Encoding.UTF8.GetBytes(jwtSettings["SecurityKey"]!);

                _logger.LogInformation($"=== API CONFIGURATION ===");
                _logger.LogInformation($"Configured Issuer: {jwtSettings["Issuer"]}");
                _logger.LogInformation($"Configured Audience: {jwtSettings["Audience"]}");
                _logger.LogInformation($"SecurityKey present: {!string.IsNullOrEmpty(jwtSettings["SecurityKey"])}");
                _logger.LogInformation($"AccessTokenExpirationMinutes: {jwtSettings["AccessTokenExpirationMinutes"]}");

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                _logger.LogInformation($"=== ATTEMPTING VALIDATION ===");
                _logger.LogInformation($"ValidateIssuer: {validationParameters.ValidateIssuer}");
                _logger.LogInformation($"ValidateAudience: {validationParameters.ValidateAudience}");
                _logger.LogInformation($"ValidateLifetime: {validationParameters.ValidateLifetime}");
                _logger.LogInformation($"ClockSkew: {validationParameters.ClockSkew}");
                _logger.LogInformation($"Current API UTC: {DateTime.UtcNow:u}");

                try
                {
                    var principal = handler.ValidateToken(request.Token, validationParameters, out var validatedToken);

                    _logger.LogInformation($"=== VALIDATION SUCCESS ===");
                    _logger.LogInformation($"Token validated successfully!");
                    _logger.LogInformation($"Principal Identity: {principal.Identity?.Name}");
                    _logger.LogInformation($"ValidatedToken Type: {validatedToken.GetType().Name}");

                    return Ok(new
                    {
                        IsValid = true,
                        Message = "Token is valid",
                        TokenDetails = new
                        {
                            Issuer = jwtToken.Issuer,
                            Audience = string.Join(", ", jwtToken.Audiences),
                            ValidFrom = jwtToken.ValidFrom,
                            ValidTo = jwtToken.ValidTo,
                            Subject = jwtToken.Subject,
                            TokenAge = DateTime.UtcNow - jwtToken.ValidFrom,
                            TimeUntilExpiry = jwtToken.ValidTo - DateTime.UtcNow
                        },
                        Claims = jwtToken.Claims.Select(c => new { c.Type, c.Value })
                    });
                }
                catch (SecurityTokenExpiredException ex)
                {
                    _logger.LogError($"=== VALIDATION FAILED: TOKEN EXPIRED ===");
                    _logger.LogError($"Exception: {ex.Message}");
                    _logger.LogError($"Token ValidTo: {jwtToken.ValidTo:u}");
                    _logger.LogError($"Current API UTC: {DateTime.UtcNow:u}");
                    _logger.LogError($"Time difference: {DateTime.UtcNow - jwtToken.ValidTo}");
                    _logger.LogError($"Is token expired? {DateTime.UtcNow > jwtToken.ValidTo}");

                    return BadRequest(new
                    {
                        IsValid = false,
                        Error = "TokenExpired",
                        Message = ex.Message,
                        Details = new
                        {
                            TokenValidTo = jwtToken.ValidTo,
                            ServerUtcNow = DateTime.UtcNow,
                            TimeDifference = DateTime.UtcNow - jwtToken.ValidTo,
                            TokenAge = DateTime.UtcNow - jwtToken.ValidFrom
                        }
                    });
                }
                catch (SecurityTokenNotYetValidException ex)
                {
                    _logger.LogError($"=== VALIDATION FAILED: TOKEN NOT YET VALID ===");
                    _logger.LogError($"Exception: {ex.Message}");
                    _logger.LogError($"Token ValidFrom: {jwtToken.ValidFrom:u}");
                    _logger.LogError($"Current API UTC: {DateTime.UtcNow:u}");
                    _logger.LogError($"Time until valid: {jwtToken.ValidFrom - DateTime.UtcNow}");

                    return BadRequest(new
                    {
                        IsValid = false,
                        Error = "TokenNotYetValid",
                        Message = ex.Message,
                        Details = new
                        {
                            TokenValidFrom = jwtToken.ValidFrom,
                            ServerUtcNow = DateTime.UtcNow
                        }
                    });
                }
                catch (SecurityTokenInvalidIssuerException ex)
                {
                    _logger.LogError($"=== VALIDATION FAILED: INVALID ISSUER ===");
                    _logger.LogError($"Exception: {ex.Message}");
                    _logger.LogError($"Token Issuer: {jwtToken.Issuer}");
                    _logger.LogError($"Expected Issuer: {validationParameters.ValidIssuer}");

                    return BadRequest(new
                    {
                        IsValid = false,
                        Error = "InvalidIssuer",
                        Message = ex.Message,
                        Details = new
                        {
                            TokenIssuer = jwtToken.Issuer,
                            ExpectedIssuer = validationParameters.ValidIssuer
                        }
                    });
                }
                catch (SecurityTokenInvalidAudienceException ex)
                {
                    _logger.LogError($"=== VALIDATION FAILED: INVALID AUDIENCE ===");
                    _logger.LogError($"Exception: {ex.Message}");
                    _logger.LogError($"Token Audiences: {string.Join(", ", jwtToken.Audiences)}");
                    _logger.LogError($"Expected Audience: {validationParameters.ValidAudience}");

                    return BadRequest(new
                    {
                        IsValid = false,
                        Error = "InvalidAudience",
                        Message = ex.Message,
                        Details = new
                        {
                            TokenAudiences = jwtToken.Audiences,
                            ExpectedAudience = validationParameters.ValidAudience
                        }
                    });
                }
                catch (SecurityTokenInvalidSignatureException ex)
                {
                    _logger.LogError($"=== VALIDATION FAILED: INVALID SIGNATURE ===");
                    _logger.LogError($"Exception: {ex.Message}");
                    _logger.LogError($"Token Algorithm: {jwtToken.Header.Alg}");
                    _logger.LogError($"SecurityKey length: {key.Length} bytes");

                    return BadRequest(new
                    {
                        IsValid = false,
                        Error = "InvalidSignature",
                        Message = ex.Message
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"=== VALIDATION FAILED: UNKNOWN ERROR ===");
                    _logger.LogError($"Exception Type: {ex.GetType().Name}");
                    _logger.LogError($"Exception Message: {ex.Message}");

                    return BadRequest(new
                    {
                        IsValid = false,
                        Error = ex.GetType().Name,
                        Message = ex.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in token validation diagnostic");
                return StatusCode(500, new { Message = "Error validating token", Error = ex.Message });
            }
        }

        // Add a class for the validation request
        public class TokenValidationRequest
        {
            public string Token { get; set; } = string.Empty;
        }

        // Rest of your existing methods...
        // [HttpPost("register")], [HttpPost("revoke")], etc.

        private static bool VerifyPassword(string? passwordHash, string password)
        {
            return passwordHash is not null &&
                   _passwordHasher.VerifyHashedPassword(null, passwordHash, password)
                   != PasswordVerificationResult.Failed;
        }

        private string GetIpAddress()
        {
            if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
                return forwardedFor.ToString();

            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }
    }
}