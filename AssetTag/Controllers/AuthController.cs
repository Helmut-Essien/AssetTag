using AssetTag.Data;
using AssetTag.Models;
using AssetTag.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace AssetTag.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(UserManager<ApplicationUser> userManager, ApplicationDbContext context, ITokenService tokenService,IEmailService emailService, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _context = context;
            _tokenService = tokenService;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

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
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }
            return Ok("User registered successfully.");
        }

        //[HttpPost("login")]
        //public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        //{
        //    var user = await _userManager.Users
        //        .Include(u => u.RefreshTokens)
        //        .SingleOrDefaultAsync(u => u.Email == dto.Email);

        //    if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
        //    {
        //        return Unauthorized(new { Message = "Invalid email or password." });
        //    }

        //    var roles = await _userManager.GetRolesAsync(user);
        //    var accessToken = _tokenService.CreateAccessToken(user, roles);

        //    var refreshToken = _tokenService.CreateRefreshToken(GetIpAddress());

        //    user.RefreshTokens.Add(refreshToken);
        //    await _userManager.UpdateAsync(user);

        //    return Ok(new TokenResponseDTO(accessToken, refreshToken.Token));
        //}


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            try
            {
                // Single query that gets everything needed
                var userData = await _userManager.Users
                    .AsNoTracking()
                    .Where(u => u.Email == dto.Email)
                    .Select(u => new
                    {
                        u.Id,
                        u.PasswordHash,
                        u.IsActive,
                        u.UserName,
                        u.Email,
                        u.SecurityStamp
                    })
                    .FirstOrDefaultAsync();

                if (userData == null)
                {
                    return Unauthorized(new { Message = "Invalid email or password." });
                }

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
                {
                    return Unauthorized(new { Message = "Invalid email or password." });
                }

                // Get roles separately (avoid DataReader conflict)
                var userForRoles = await _userManager.FindByIdAsync(userData.Id);
                var roles = await _userManager.GetRolesAsync(userForRoles);

                // Create tokens
                var accessToken = _tokenService.CreateAccessToken(userForRoles, roles);
                var refreshToken = _tokenService.CreateRefreshToken(GetIpAddress());

                // Add refresh token
                userForRoles.RefreshTokens.Add(refreshToken);
                await _userManager.UpdateAsync(userForRoles);

                return Ok(new TokenResponseDTO(accessToken, refreshToken.Token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for {Email}", dto.Email);
                return StatusCode(500, new { Message = "An error occurred during login." });
            }
        }

        // Static password verifier for performance
        private static readonly PasswordHasher<ApplicationUser> _passwordHasher = new();
        private static bool VerifyPassword(string? passwordHash, string password)
        {
            return passwordHash != null &&
                   _passwordHasher.VerifyHashedPassword(null, passwordHash, password)
                   != PasswordVerificationResult.Failed;
        }



        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenResponseDTO dto)
        {
            // Single query to get user with active refresh token
            var user = await _userManager.Users
                .Include(u => u.RefreshTokens)
                .Where(u => u.RefreshTokens.Any(t => t.Token == dto.RefreshToken && t.IsActive))
                .Select(u => new { User = u, Token = u.RefreshTokens.First(t => t.Token == dto.RefreshToken) })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return Unauthorized(new { Message = "Invalid refresh token." });
            }

            // Revoke old token
            user.Token.Revoked = DateTime.UtcNow;
            user.Token.RevokedByIp = GetIpAddress();

            // Create new refresh token
            var newRefreshToken = _tokenService.CreateRefreshToken(GetIpAddress());
            user.Token.ReplacedByToken = newRefreshToken.Token;
            user.User.RefreshTokens.Add(newRefreshToken);

            await _userManager.UpdateAsync(user.User);

            // Generate new access token
            var roles = await _userManager.GetRolesAsync(user.User);
            var newAccessToken = _tokenService.CreateAccessToken(user.User, roles);

            return Ok(new TokenResponseDTO(newAccessToken, newRefreshToken.Token));
        }

        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke([FromBody] TokenResponseDTO dto)
        {
            var token = dto.RefreshToken;
            var user = await _userManager.Users
                .Include(u => u.RefreshTokens)
                .SingleOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == token));

            if (user == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            var existing = user.RefreshTokens.Single(t => t.Token == token);
            if (!existing.IsActive)
            {
                return BadRequest(new { Message = "This token is already revoked." });
            }
            existing.Revoked = DateTime.UtcNow;
            existing.RevokedByIp = GetIpAddress();

            await _userManager.UpdateAsync(user);
            return Ok(new { Message = "Token revoked successfully." });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                // Don't reveal that the user doesn't exist for security reasons
                return Ok(new { Message = "If the email exists, a password reset link has been sent.(Syke)" });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Send email with reset token
            var frontendBaseUrl = _configuration["FrontendBaseUrl"] ?? "https://1qtrdwgx-44369.uks1.devtunnels.ms"; // Your Portal URL
            var resetUrl = $"{frontendBaseUrl}/Account/ResetPassword";

            var emailSent = await _emailService.SendPasswordResetEmailAsync(user.Email, token, resetUrl);

            if (!emailSent)
            {
                // Log the error but still return success for security
                _logger.LogWarning("Failed to send password reset email to {Email}", user.Email);
            }

            return Ok(new
            {
                Message = "If the email exists, a password reset link has been sent."
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                // Don't reveal that the user doesn't exist
                return BadRequest(new { Message = "Invalid reset token." });
            }

            var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    Message = "Failed to reset password.",
                    Errors = result.Errors.Select(e => e.Description)
                });
            }

            // Optionally, revoke all refresh tokens for security
            var refreshTokens = user.RefreshTokens.Where(rt => rt.IsActive).ToList();
            foreach (var token in refreshTokens)
            {
                token.Revoked = DateTime.UtcNow;
                token.RevokedByIp = GetIpAddress();
            }

            await _userManager.UpdateAsync(user);

            return Ok(new { Message = "Password has been reset successfully." });
        }


        [HttpPost("validate-invitation")]
        [AllowAnonymous]
        public async Task<ActionResult> ValidateInvitation([FromBody] ValidateInvitationDTO dto)
        {
            try
            {
                var invitation = await _context.Invitations
                    .FirstOrDefaultAsync(i => i.Token == dto.Token && !i.IsUsed);

                if (invitation == null)
                {
                    return BadRequest(new { Message = "Invalid or expired invitation token." });
                }

                if (invitation.ExpiresAt < DateTime.UtcNow)
                {
                    return BadRequest(new { Message = "This invitation has expired." });
                }

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

                if (invitation == null)
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
                var existingUser = await _userManager.FindByEmailAsync(dto.Email);
                if (existingUser != null)
                {
                    _logger.LogWarning("User already exists with email: {Email}", dto.Email);
                    return BadRequest(new { Message = "A user with this email already exists." });
                }

                // Check if username is already taken
                var existingUsername = await _userManager.FindByNameAsync(dto.Username);
                if (existingUsername != null)
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
                _context.Invitations.Update(invitation);
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

        private string GetIpAddress()
        {
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                return Request.Headers["X-Forwarded-For"].ToString();
            }
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }
    }
}
