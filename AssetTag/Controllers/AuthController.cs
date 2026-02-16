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
using Shared.Models;
using AssetTag.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using System.IdentityModel.Tokens.Jwt;
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

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
        {
            _logger.LogInformation("Registration attempt for username: {Username}, email: {Email}",
                dto.Username, dto.Email);

            var user = new ApplicationUser
            {
                UserName = dto.Username,
                Email = dto.Email,
                FirstName = dto.FirstName,
                Surname = dto.Surname
            };

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Registration failed for email {Email}. Errors: {Errors}",
                    dto.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                return BadRequest(result.Errors);
            }

            _logger.LogInformation("User registered successfully: {UserId} - {Email}", user.Id, dto.Email);
            return Ok("User registered successfully.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            try
            {
                _logger.LogInformation("Login attempt for email: {Email}", dto.Email);

                // Single optimized query with projection
                var userData = await _userManager.Users
                    .AsNoTracking()
                    .Where(u => u.Email == dto.Email)
                    .Select(u => new { u.Id, u.PasswordHash, u.IsActive, u.UserName, u.Email })
                    .FirstOrDefaultAsync();

                if (userData is null)
                {
                    _logger.LogWarning("Login failed: User not found with email {Email}", dto.Email);
                    return Unauthorized(new { Message = "Invalid email or password." });
                }

                _logger.LogDebug("User found: {UserId} - {Email}", userData.Id, userData.Email);

                // Check deactivation status
                if (!userData.IsActive)
                {
                    _logger.LogWarning("Login blocked: Account deactivated for user {UserId}", userData.Id);
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
                    _logger.LogWarning("Login failed: Invalid password for user {UserId}", userData.Id);
                    return Unauthorized(new { Message = "Invalid email or password." });
                }

                _logger.LogDebug("Password verified successfully for user {UserId}", userData.Id);

                // Get user for roles and token operations
                var user = await _userManager.FindByIdAsync(userData.Id);
                if (user is null)
                {
                    _logger.LogError("User not found after initial query for Id {UserId}", userData.Id);
                    return Unauthorized(new { Message = "User not found." });
                }

                // Parallel operations for maximum performance
                var rolesTask = _userManager.GetRolesAsync(user);
                var refreshTokenTask = Task.Run(() => _tokenService.CreateRefreshToken(GetIpAddress()));
                await Task.WhenAll(rolesTask, refreshTokenTask);

                var roles = await rolesTask;
                var refreshToken = await refreshTokenTask;

                // Add refresh token and update
                user.RefreshTokens.Add(refreshToken);
                await _userManager.UpdateAsync(user);

                // Create access token
                var accessToken = _tokenService.CreateAccessToken(user, roles);

                // Log token details (excluding sensitive parts)
                LogAccessTokenDetails(accessToken, userData.Id, roles);

                _logger.LogInformation("Login successful for user {UserId}. Access token generated.", userData.Id);

                return Ok(new TokenResponseDTO(accessToken, refreshToken.Token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for {Email}", dto.Email);
                return StatusCode(500, new { Message = "An error occurred during login." });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenResponseDTO dto)
        {
            try
            {
                _logger.LogInformation("Refresh token request received from IP: {IP}", GetIpAddress());

                if (string.IsNullOrWhiteSpace(dto.RefreshToken))
                {
                    _logger.LogWarning("Refresh token request with null/empty token");
                    return Unauthorized(new { Message = "Refresh token is required." });
                }

                // Also validate access token if provided (optional for refresh)
                if (!string.IsNullOrEmpty(dto.AccessToken))
                {
                    var tokenValidation = ValidateAccessToken(dto.AccessToken);
                    if (!tokenValidation.IsValid)
                    {
                        _logger.LogWarning("Invalid access token provided for refresh: {ValidationMessage}",
                            tokenValidation.ValidationMessage);
                    }
                    else
                    {
                        _logger.LogDebug("Access token validation passed during refresh");
                    }
                }

                // Log token length for debugging (not the actual token)
                _logger.LogDebug("Refresh token length: {Length}", dto.RefreshToken.Length);

                // Fast validation query first
                var isValidToken = await _context.RefreshTokens
                    .AsNoTracking()
                    .AnyAsync(rt => rt.Token == dto.RefreshToken &&
                                   rt.Revoked == null &&
                                   rt.Expires > DateTime.UtcNow);

                if (!isValidToken)
                {
                    _logger.LogWarning("Invalid or expired refresh token");

                    // Check if token exists at all
                    var tokenExists = await _context.RefreshTokens
                        .AsNoTracking()
                        .AnyAsync(rt => rt.Token == dto.RefreshToken);

                    if (!tokenExists)
                    {
                        _logger.LogWarning("Refresh token not found in database");
                    }
                    else
                    {
                        // Token exists but is invalid - check why
                        var tokenInfo = await _context.RefreshTokens
                            .AsNoTracking()
                            .Where(rt => rt.Token == dto.RefreshToken)
                            .Select(rt => new { rt.Revoked, rt.Expires })
                            .FirstOrDefaultAsync();

                        if (tokenInfo?.Revoked != null)
                        {
                            _logger.LogWarning("Refresh token was revoked at {RevokedAt}", tokenInfo.Revoked);
                        }
                        else if (tokenInfo?.Expires < DateTime.UtcNow)
                        {
                            _logger.LogWarning("Refresh token expired at {ExpiresAt}. Current time: {CurrentTime}",
                                tokenInfo.Expires, DateTime.UtcNow);
                        }
                    }

                    return Unauthorized(new { Message = "Invalid or expired refresh token." });
                }

                // Get full token with user for update
                var refreshToken = await _context.RefreshTokens
                    .Include(rt => rt.ApplicationUser)
                    .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken);

                if (refreshToken?.ApplicationUser is null)
                {
                    _logger.LogError("Refresh token found but user is null");
                    return Unauthorized(new { Message = "Token or user not found." });
                }

                var user = refreshToken.ApplicationUser;

                // Check if user is still active
                if (!user.IsActive)
                {
                    _logger.LogWarning("Refresh attempt for inactive user {UserId}", user.Id);
                    return Unauthorized(new { Message = "User account is deactivated." });
                }

                _logger.LogInformation("Valid refresh token for user {UserId}", user.Id);

                // Parallel operations for maximum performance
                var rolesTask = _userManager.GetRolesAsync(user);
                var newRefreshTokenTask = Task.Run(() => _tokenService.CreateRefreshToken(GetIpAddress()));
                await Task.WhenAll(rolesTask, newRefreshTokenTask);

                var roles = await rolesTask;
                var newRefreshToken = await newRefreshTokenTask;

                // Update operations
                refreshToken.Revoked = DateTime.UtcNow;
                refreshToken.RevokedByIp = GetIpAddress();
                refreshToken.ReplacedByToken = newRefreshToken.Token;

                // Add new token directly to context for better performance
                newRefreshToken.ApplicationUserId = user.Id;
                _context.RefreshTokens.Add(newRefreshToken);

                // Single save operation for both updates
                await _context.SaveChangesAsync();

                // Create access token
                var newAccessToken = _tokenService.CreateAccessToken(user, roles);

                // Log new token details
                LogAccessTokenDetails(newAccessToken, user.Id, roles);

                _logger.LogInformation("Successfully refreshed tokens for user {UserId}", user.Id);

                return Ok(new TokenResponseDTO(newAccessToken, newRefreshToken.Token));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error during token refresh");
                return StatusCode(500, new { Message = "A database error occurred." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token refresh");
                return StatusCode(500, new { Message = "An error occurred while refreshing token." });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] TokenResponseDTO dto)
        {
            try
            {
                _logger.LogInformation("Logout request from IP: {IP}", GetIpAddress());

                // Get user ID from token claims
                var userId = User.FindFirst("sub")?.Value
                            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Logout: Unable to determine user ID from token");
                    return BadRequest(new { Message = "Invalid token." });
                }

                // Revoke the specific refresh token if provided
                if (!string.IsNullOrEmpty(dto.RefreshToken))
                {
                    var affectedRows = await _context.Database.ExecuteSqlRawAsync(
                        @"UPDATE RefreshTokens
                          SET Revoked = {0}, RevokedByIp = {1}
                          WHERE Token = {2} AND ApplicationUserId = {3} AND Revoked IS NULL",
                        DateTime.UtcNow, GetIpAddress(), dto.RefreshToken, userId);

                    _logger.LogInformation("Logout: Revoked {Rows} refresh token(s) for user {UserId}",
                        affectedRows, userId);
                }
                else
                {
                    // If no specific token provided, revoke all tokens for this user
                    var affectedRows = await _context.Database.ExecuteSqlRawAsync(
                        @"UPDATE RefreshTokens
                          SET Revoked = {0}, RevokedByIp = {1}
                          WHERE ApplicationUserId = {2} AND Revoked IS NULL",
                        DateTime.UtcNow, GetIpAddress(), userId);

                    _logger.LogInformation("Logout: Revoked all {Rows} refresh token(s) for user {UserId}",
                        affectedRows, userId);
                }

                return Ok(new { Message = "Logged out successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { Message = "An error occurred during logout." });
            }
        }

        [HttpPost("validate-token")]
        [Authorize]
        public IActionResult ValidateToken()
        {
            try
            {
                var authorizationHeader = Request.Headers["Authorization"].ToString();

                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    _logger.LogWarning("Token validation failed: No Bearer token in Authorization header");
                    return Unauthorized(new
                    {
                        Message = "No token provided",
                        Details = "Authorization header missing or malformed"
                    });
                }

                var token = authorizationHeader.Substring("Bearer ".Length).Trim();

                var validationResult = ValidateAccessToken(token);

                if (validationResult.IsValid)
                {
                    _logger.LogInformation("Token validation successful for user {UserId}",
                        validationResult.UserId);

                    return Ok(new
                    {
                        IsValid = true,
                        UserId = validationResult.UserId,
                        UserName = validationResult.UserName,
                        Email = validationResult.Email,
                        Roles = validationResult.Roles,
                        ExpiresAt = validationResult.ExpiresAt,
                        IssuedAt = validationResult.IssuedAt
                    });
                }
                else
                {
                    _logger.LogWarning("Token validation failed: {ValidationMessage}",
                        validationResult.ValidationMessage);

                    return Unauthorized(new
                    {
                        IsValid = false,
                        Message = validationResult.ValidationMessage,
                        Details = validationResult.DetailedMessage
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token validation");
                return StatusCode(500, new { Message = "Error validating token" });
            }
        }

        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke([FromBody] TokenResponseDTO dto)
        {
            try
            {
                _logger.LogInformation("Revoke token request from IP: {IP}", GetIpAddress());

                // Validate token before revoking
                if (!string.IsNullOrEmpty(dto.AccessToken))
                {
                    var tokenValidation = ValidateAccessToken(dto.AccessToken);
                    _logger.LogDebug("Token validation before revoke: {IsValid} - {Message}",
                        tokenValidation.IsValid, tokenValidation.ValidationMessage);
                }

                // Optimized: Direct SQL execution for maximum performance
                var affectedRows = await _context.Database.ExecuteSqlRawAsync(
                    @"UPDATE RefreshTokens 
                      SET Revoked = {0}, RevokedByIp = {1} 
                      WHERE Token = {2} AND Revoked IS NULL",
                    DateTime.UtcNow, GetIpAddress(), dto.RefreshToken);

                if (affectedRows > 0)
                {
                    _logger.LogInformation("Token revoked successfully. Affected rows: {Rows}", affectedRows);
                    return Ok(new { Message = "Token revoked successfully." });
                }
                else
                {
                    _logger.LogWarning("Token not found or already revoked: {TokenPrefix}...",
                        dto.RefreshToken?.Substring(0, Math.Min(8, dto.RefreshToken?.Length ?? 0)));
                    return BadRequest(new { Message = "Token not found or already revoked." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                return StatusCode(500, new { Message = "An error occurred while revoking token." });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO dto)
        {
            try
            {
                _logger.LogInformation("Forgot password request for email: {Email}", dto.Email);

                // Single query with only needed fields
                var user = await _userManager.Users
                    .AsNoTracking()
                    .Where(u => u.Email == dto.Email)
                    .Select(u => new { u.Id, u.Email })
                    .FirstOrDefaultAsync();

                if (user is null)
                {
                    _logger.LogWarning("Forgot password: User not found with email {Email}", dto.Email);
                    // Return same message for security - don't reveal user existence
                    return Ok(new { Message = "If the email exists, a password reset link has been sent." });
                }

                _logger.LogInformation("Generating password reset token for user {UserId}", user.Id);

                // Generate token
                var fullUser = await _userManager.FindByIdAsync(user.Id);
                var token = await _userManager.GeneratePasswordResetTokenAsync(fullUser);

                // Prepare email data
                var frontendBaseUrl = _configuration["FrontendBaseUrl"] ?? "https://1qtrdwgx-44369.uks1.devtunnels.ms";
                var resetUrl = $"{frontendBaseUrl}/Account/ResetPassword";

                // Fire and forget email for performance
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendPasswordResetEmailAsync(user.Email, token, resetUrl);
                        _logger.LogInformation("Password reset email sent to {Email}", user.Email);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send password reset email to {Email}", user.Email);
                    }
                });

                return Ok(new { Message = "If the email exists, a password reset link has been sent." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in forgot password for {Email}", dto.Email);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO dto)
        {
            try
            {
                _logger.LogInformation("Reset password request for email: {Email}", dto.Email);

                // Find user with minimal data
                var user = await _userManager.FindByEmailAsync(dto.Email);
                if (user is null)
                {
                    _logger.LogWarning("Reset password: User not found with email {Email}", dto.Email);
                    // Don't reveal user existence
                    return BadRequest(new { Message = "Invalid reset token." });
                }

                _logger.LogInformation("Resetting password for user {UserId}", user.Id);

                // Reset password
                var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
                if (!result.Succeeded)
                {
                    _logger.LogWarning("Password reset failed for user {UserId}. Errors: {Errors}",
                        user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));

                    return BadRequest(new
                    {
                        Message = "Failed to reset password.",
                        Errors = result.Errors.Select(e => e.Description)
                    });
                }

                _logger.LogInformation("Password successfully reset for user {UserId}", user.Id);

                // Revoke all active tokens in background for security
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var revokedRows = await _context.Database.ExecuteSqlRawAsync(
                            @"UPDATE RefreshTokens 
                              SET Revoked = {0}, RevokedByIp = {1} 
                              WHERE ApplicationUserId = {2} AND Revoked IS NULL",
                            DateTime.UtcNow, GetIpAddress(), user.Id);

                        _logger.LogInformation("Revoked {Count} tokens for user {UserId} after password reset",
                            revokedRows, user.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to revoke tokens for user {UserId}", user.Id);
                    }
                });

                return Ok(new { Message = "Password has been reset successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for {Email}", dto.Email);
                return StatusCode(500, new { Message = "An error occurred while resetting password." });
            }
        }

        [HttpPost("validate-invitation")]
        [AllowAnonymous]
        public async Task<ActionResult> ValidateInvitation([FromBody] ValidateInvitationDTO dto)
        {
            try
            {
                _logger.LogInformation("Validating invitation token: {Token}", dto.Token);

                var invitation = await _context.Invitations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Token == dto.Token && !i.IsUsed);

                if (invitation is null)
                {
                    _logger.LogWarning("Invalid or used invitation token: {Token}", dto.Token);
                    return BadRequest(new { Message = "Invalid or expired invitation token." });
                }

                if (invitation.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogWarning("Invitation token expired: {Token}. Expired at: {ExpiresAt}",
                        dto.Token, invitation.ExpiresAt);
                    return BadRequest(new { Message = "This invitation has expired." });
                }

                _logger.LogInformation("Invitation token valid for email: {Email}, role: {Role}",
                    invitation.Email, invitation.Role);

                return Ok(new
                {
                    Message = "Invitation is valid.",
                    Email = invitation.Email,
                    Role = invitation.Role
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating invitation token");
                return StatusCode(500, new { Message = "An internal error occurred." });
            }
        }

        [HttpPost("register-with-invitation")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterWithInvitation([FromBody] RegisterWithInvitationDTO dto)
        {
            try
            {
                _logger.LogInformation("Starting registration with invitation for email: {Email}", dto.Email);

                // Validate invitation
                var invitation = await _context.Invitations
                    .FirstOrDefaultAsync(i => i.Token == dto.Token && !i.IsUsed);

                if (invitation is null)
                {
                    _logger.LogWarning("Invalid or used invitation token: {Token}", dto.Token);
                    return BadRequest(new { Message = "Invalid or expired invitation token." });
                }

                if (invitation.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogWarning("Expired invitation token: {Token}, expired at: {ExpiresAt}",
                        dto.Token, invitation.ExpiresAt);
                    return BadRequest(new { Message = "This invitation has expired." });
                }

                // Check if email matches
                if (!string.Equals(invitation.Email, dto.Email, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Email mismatch for invitation. Expected: {Expected}, Got: {Actual}",
                        invitation.Email, dto.Email);
                    return BadRequest(new { Message = "Email does not match the invitation." });
                }

                // Check if user already exists
                if (await _userManager.FindByEmailAsync(dto.Email) is not null)
                {
                    _logger.LogWarning("User already exists with email: {Email}", dto.Email);
                    return BadRequest(new { Message = "A user with this email already exists." });
                }

                // Check if username is already taken
                if (await _userManager.FindByNameAsync(dto.Username) is not null)
                {
                    _logger.LogWarning("Username already taken: {Username}", dto.Username);
                    return BadRequest(new { Message = "Username is already taken. Please choose a different username." });
                }

                // Create user
                var user = new ApplicationUser
                {
                    UserName = dto.Username,
                    Email = dto.Email,
                    FirstName = dto.FirstName,
                    Surname = dto.Surname,
                    OtherNames = dto.OtherNames,
                    DateOfBirth = dto.DateOfBirth,
                    Address = dto.Address,
                    JobRole = dto.JobRole,
                    DepartmentId = dto.DepartmentId,
                    IsActive = true,
                    DateCreated = DateTime.UtcNow
                };

                _logger.LogInformation("Creating user: {Username}, {Email}", dto.Username, dto.Email);

                var result = await _userManager.CreateAsync(user, dto.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to create user {Email}. Errors: {Errors}", dto.Email, errors);

                    return BadRequest(new
                    {
                        Message = "Failed to create user account.",
                        Errors = result.Errors.Select(e => e.Description)
                    });
                }

                // Assign role from invitation
                if (!string.IsNullOrEmpty(invitation.Role))
                {
                    _logger.LogInformation("Assigning role {Role} to user {Email}", invitation.Role, dto.Email);
                    var roleResult = await _userManager.AddToRoleAsync(user, invitation.Role);
                    if (!roleResult.Succeeded)
                    {
                        var roleErrors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                        _logger.LogWarning("Failed to assign role {Role} to user {Email}. Errors: {Errors}",
                            invitation.Role, dto.Email, roleErrors);
                        // Continue anyway - user is created but role assignment failed
                    }
                }

                // Mark invitation as used
                invitation.IsUsed = true;
                invitation.UsedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully registered user with invitation: {Email} - UserId: {UserId}",
                    dto.Email, user.Id);

                return Ok(new { Message = "User registered successfully. You can now login." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user with invitation for {Email}", dto.Email);
                return StatusCode(500, new { Message = "An internal error occurred during registration." });
            }
        }

        #region Token Validation Helper Methods

        private (bool IsValid, string ValidationMessage, string DetailedMessage,
            string UserId, string UserName, string Email, List<string> Roles,
            DateTime? ExpiresAt, DateTime? IssuedAt) ValidateAccessToken(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    return (false, "Token is empty", "No token provided",
                        null, null, null, null, null, null);
                }

                var handler = new JwtSecurityTokenHandler();

                // Check if token can be read
                if (!handler.CanReadToken(token))
                {
                    return (false, "Invalid token format",
                        "Token cannot be parsed as JWT. Make sure it's a valid JWT token.",
                        null, null, null, null, null, null);
                }

                JwtSecurityToken jwtToken;
                try
                {
                    jwtToken = handler.ReadJwtToken(token);
                }
                catch (ArgumentException ex)
                {
                    return (false, "Malformed token",
                        $"Token parsing failed: {ex.Message}",
                        null, null, null, null, null, null);
                }
                catch (Exception ex)
                {
                    return (false, "Token parsing error",
                        $"Unexpected error parsing token: {ex.Message}",
                        null, null, null, null, null, null);
                }

                // Check token structure
                if (jwtToken == null)
                {
                    return (false, "Token is null",
                        "Token parsed to null",
                        null, null, null, null, null, null);
                }

                // Check if token has expired
                if (jwtToken.ValidTo < DateTime.UtcNow)
                {
                    var expiryTime = jwtToken.ValidTo;
                    var currentTime = DateTime.UtcNow;
                    var timeDifference = currentTime - expiryTime;

                    return (false, "Token has expired",
                        $"Token expired at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC. Current time: {currentTime:yyyy-MM-dd HH:mm:ss} UTC. Expired {timeDifference.TotalMinutes:F2} minutes ago.",
                        null, null, null, null, expiryTime, null);
                }

                // Check if token is not yet valid
                if (jwtToken.ValidFrom > DateTime.UtcNow)
                {
                    return (false, "Token not yet valid",
                        $"Token will be valid from {jwtToken.ValidFrom:yyyy-MM-dd HH:mm:ss} UTC",
                        null, null, null, null, null, jwtToken.ValidFrom);
                }

                // Extract claims
                var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                var userName = jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;
                var email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
                var roles = jwtToken.Claims
                    .Where(c => c.Type == "role" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                    .Select(c => c.Value)
                    .ToList();

                // Check essential claims
                if (string.IsNullOrEmpty(userId))
                {
                    return (false, "Missing user identifier",
                        "Token does not contain 'sub' claim (user identifier)",
                        null, null, null, null, jwtToken.ValidTo, jwtToken.ValidFrom);
                }

                if (string.IsNullOrEmpty(userName))
                {
                    _logger.LogWarning("Token missing username claim for user {UserId}", userId);
                }

                // Log token details
                _logger.LogDebug("Token validation - UserId: {UserId}, UserName: {UserName}, " +
                               "Expires: {Expires}, Issued: {Issued}, Roles: {Roles}",
                               userId, userName, jwtToken.ValidTo, jwtToken.IssuedAt,
                               string.Join(", ", roles));

                return (true, "Token is valid", "Token passed all validation checks",
                    userId, userName, email, roles, jwtToken.ValidTo, jwtToken.IssuedAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token validation error");
                return (false, "Token validation error",
                    $"Exception during token validation: {ex.Message}",
                    null, null, null, null, null, null);
            }
        }

        private void LogAccessTokenDetails(string token, string userId, IList<string> roles)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();

                if (handler.CanReadToken(token))
                {
                    var jwtToken = handler.ReadJwtToken(token);
                    var expiresIn = (jwtToken.ValidTo - DateTime.UtcNow).TotalMinutes;

                    _logger.LogInformation("Access Token Details - UserId: {UserId}, " +
                                         "Issued: {IssuedAt}, Expires: {ExpiresAt} (in {ExpiresIn:F1} minutes), " +
                                         "Roles: {Roles}",
                                         userId,
                                         jwtToken.IssuedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                                         jwtToken.ValidTo.ToString("yyyy-MM-dd HH:mm:ss"),
                                         expiresIn,
                                         string.Join(", ", roles));
                }
                else
                {
                    _logger.LogWarning("Cannot read access token for user {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log access token details for user {UserId}", userId);
            }
        }

        #endregion

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

