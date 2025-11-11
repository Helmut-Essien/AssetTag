using AssetTag.Data;
using Shared.DTOs;
using AssetTag.Models;
using AssetTag.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            var user = await _userManager.Users
                .Include(u => u.RefreshTokens)
                .SingleOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
                {
                return Unauthorized(new { Message = "Invalid email or password." });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _tokenService.CreateAccessToken(user, roles);

            var refreshToken = _tokenService.CreateRefreshToken(GetIpAddress());

            user.RefreshTokens.Add(refreshToken);
            await _userManager.UpdateAsync(user);

            return Ok (new TokenResponseDTO(accessToken, refreshToken.Token));
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenResponseDTO dto)
        {
            var refreshToken = dto.RefreshToken;
            var user = await _userManager.Users
                .Include(u => u.RefreshTokens)
                .SingleOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == refreshToken));

            if (user == null)
                {
                return Unauthorized(new { Message = "Invalid refresh token." });
            }

            var existingToken = user.RefreshTokens.Single(t => t.Token == refreshToken);
            if (!existingToken.IsActive)
            {
                return Unauthorized(new { Message = "Refresh token is not active." });
            }

            existingToken.Revoked = DateTime.UtcNow;
            existingToken.RevokedByIp = GetIpAddress();

            var newRefreshToken = _tokenService.CreateRefreshToken(GetIpAddress());
            existingToken.ReplacedByToken = newRefreshToken.Token;
            user.RefreshTokens.Add(newRefreshToken);

            await _userManager.UpdateAsync(user);

            // Generate new access token
            var roles = await _userManager.GetRolesAsync(user);
            var newAccessToken = _tokenService.CreateAccessToken(user, roles);

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
