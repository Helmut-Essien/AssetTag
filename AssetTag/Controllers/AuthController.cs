using AssetTag.Data;
using AssetTag.Models;
using AssetTag.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

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
            var user = new ApplicationUser
            {
                UserName = dto.Username,
                Email = dto.Email,
                FirstName = dto.FirstName,
                Surname = dto.Surname
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            return !result.Succeeded
                ? BadRequest(result.Errors)
                : Ok("User registered successfully.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            try
            {
                // Single optimized query with projection
                var userData = await _userManager.Users
                    .AsNoTracking()
                    .Where(u => u.Email == dto.Email)
                    .Select(u => new { u.Id, u.PasswordHash, u.IsActive, u.UserName, u.Email })
                    .FirstOrDefaultAsync();

                if (userData is null)
                    return Unauthorized(new { Message = "Invalid email or password." });

                // Check deactivation status
                if (!userData.IsActive)
                {
                    return Unauthorized(new
                    {
                        Message = "Account deactivated. Contact administrator.",
                        Code = "ACCOUNT_DEACTIVATED",
                        IsDeactivated = true
                    });
                }

                // Password verification
                if (!VerifyPassword(userData.PasswordHash, dto.Password))
                    return Unauthorized(new { Message = "Invalid email or password." });

                // Get user for roles and token operations
                var user = await _userManager.FindByIdAsync(userData.Id);
                if (user is null)
                    return Unauthorized(new { Message = "User not found." });

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
                // Fast validation query first
                var isValidToken = await _context.RefreshTokens
                    .AsNoTracking()
                    .AnyAsync(rt => rt.Token == dto.RefreshToken &&
                                   rt.Revoked == null &&
                                   rt.Expires > DateTime.UtcNow);

                if (!isValidToken)
                    return Unauthorized(new { Message = "Invalid or expired refresh token." });

                // Get full token with user for update
                var refreshToken = await _context.RefreshTokens
                    .Include(rt => rt.ApplicationUser)
                    .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken);

                if (refreshToken?.ApplicationUser is null)
                    return Unauthorized(new { Message = "Token or user not found." });

                var user = refreshToken.ApplicationUser;

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

                return Ok(new TokenResponseDTO(newAccessToken, newRefreshToken.Token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, new { Message = "An error occurred while refreshing token." });
            }
        }

        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke([FromBody] TokenResponseDTO dto)
        {
            try
            {
                // Optimized: Direct SQL execution for maximum performance
                var affectedRows = await _context.Database.ExecuteSqlRawAsync(
                    @"UPDATE RefreshTokens 
                      SET Revoked = {0}, RevokedByIp = {1} 
                      WHERE Token = {2} AND Revoked IS NULL",
                    DateTime.UtcNow, GetIpAddress(), dto.RefreshToken);

                return affectedRows > 0
                    ? Ok(new { Message = "Token revoked successfully." })
                    : BadRequest(new { Message = "Token not found or already revoked." });
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
                // Single query with only needed fields
                var user = await _userManager.Users
                    .AsNoTracking()
                    .Where(u => u.Email == dto.Email)
                    .Select(u => new { u.Id, u.Email })
                    .FirstOrDefaultAsync();

                if (user is null)
                {
                    // Return same message for security - don't reveal user existence
                    return Ok(new { Message = "If the email exists, a password reset link has been sent." });
                }

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
                // Find user with minimal data
                var user = await _userManager.FindByEmailAsync(dto.Email);
                if (user is null)
                {
                    // Don't reveal user existence
                    return BadRequest(new { Message = "Invalid reset token." });
                }

                // Reset password
                var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
                if (!result.Succeeded)
                {
                    return BadRequest(new
                    {
                        Message = "Failed to reset password.",
                        Errors = result.Errors.Select(e => e.Description)
                    });
                }

                // Revoke all active tokens in background for security
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync(
                            @"UPDATE RefreshTokens 
                              SET Revoked = {0}, RevokedByIp = {1} 
                              WHERE ApplicationUserId = {2} AND Revoked IS NULL",
                            DateTime.UtcNow, GetIpAddress(), user.Id);
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
                var invitation = await _context.Invitations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Token == dto.Token && !i.IsUsed);

                if (invitation is null)
                    return BadRequest(new { Message = "Invalid or expired invitation token." });

                if (invitation.ExpiresAt < DateTime.UtcNow)
                    return BadRequest(new { Message = "This invitation has expired." });

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

                _logger.LogInformation("Successfully registered user with invitation: {Email}", dto.Email);

                return Ok(new { Message = "User registered successfully. You can now login." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user with invitation for {Email}", dto.Email);
                return StatusCode(500, new { Message = "An internal error occurred during registration." });
            }
        }

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