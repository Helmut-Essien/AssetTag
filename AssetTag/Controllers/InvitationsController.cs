using AssetTag.Data;
using Shared.Models;
using AssetTag.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AssetTag.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class InvitationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<InvitationsController> _logger;

        public InvitationsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<InvitationsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<InvitationResponseDTO>> CreateInvitation([FromBody] CreateInvitationDTO dto)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(dto.Email);
                if (existingUser != null)
                {
                    return BadRequest("A user with this email already exists.");
                }

                // Check for existing active invitation
                var existingInvitation = await _context.Invitations
                    .FirstOrDefaultAsync(i => i.Email == dto.Email && !i.IsUsed && i.ExpiresAt > DateTime.UtcNow);

                if (existingInvitation != null)
                {
                    return BadRequest("An active invitation already exists for this email.");
                }

                // Get current user
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Unauthorized();
                }

                // Create invitation
                var invitation = new Invitation
                {
                    Email = dto.Email,
                    Role = dto.Role,
                    InvitedByUserId = currentUser.Id,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(2)
                };

                _context.Invitations.Add(invitation);
                await _context.SaveChangesAsync();

                // Send invitation email
                var frontendBaseUrl = _configuration["FrontendBaseUrl"] ?? "https://1qtrdwgx-44369.uks1.devtunnels.ms";
                var invitationUrl = $"{frontendBaseUrl}/Account/Register";

                var emailSent = await _emailService.SendInvitationEmailAsync(
                    dto.Email,
                    invitation.Token,
                    invitationUrl,
                    currentUser.UserName ?? currentUser.Email ?? "Administrator");

                if (!emailSent)
                {
                    _logger.LogWarning("Failed to send invitation email to {Email}", dto.Email);
                    // Continue anyway, admin can resend later
                }

                var response = new InvitationResponseDTO
                {
                    Id = invitation.Id,
                    Email = invitation.Email,
                    Token = invitation.Token,
                    CreatedAt = invitation.CreatedAt,
                    ExpiresAt = invitation.ExpiresAt,
                    IsUsed = invitation.IsUsed,
                    Role = invitation.Role,
                    InvitedByUserName = currentUser.UserName ?? currentUser.Email ?? "Administrator"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invitation for {Email}", dto.Email);
                return StatusCode(500, "An internal error occurred.");
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InvitationResponseDTO>>> GetInvitations()
        {
            try
            {
                var invitations = await _context.Invitations
                    .Include(i => i.InvitedByUser)
                    .OrderByDescending(i => i.CreatedAt)
                    .Select(i => new InvitationResponseDTO
                    {
                        Id = i.Id,
                        Email = i.Email,
                        Token = i.Token,
                        CreatedAt = i.CreatedAt,
                        ExpiresAt = i.ExpiresAt,
                        IsUsed = i.IsUsed,
                        UsedAt = i.UsedAt,  // Now this will work
                        Role = i.Role,
                        InvitedByUserName = i.InvitedByUser != null ?
                            i.InvitedByUser.UserName ?? i.InvitedByUser.Email ?? "Unknown" : "Unknown"
                    })
                    .ToListAsync();

                return Ok(invitations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invitations");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        [HttpPost("resend/{id}")]
        public async Task<IActionResult> ResendInvitation(string id)
        {
            try
            {
                var invitation = await _context.Invitations
                    .Include(i => i.InvitedByUser)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (invitation == null)
                {
                    return NotFound("Invitation not found.");
                }

                if (invitation.IsUsed)
                {
                    return BadRequest("This invitation has already been used.");
                }

                if (invitation.ExpiresAt < DateTime.UtcNow)
                {
                    return BadRequest("This invitation has expired.");
                }

                // Get current user for audit
                var currentUser = await _userManager.GetUserAsync(User);

                // Send invitation email
                var frontendBaseUrl = _configuration["FrontendBaseUrl"] ?? "https://1qtrdwgx-44369.uks1.devtunnels.ms";
                var invitationUrl = $"{frontendBaseUrl}/Account/Register";

                var emailSent = await _emailService.SendInvitationEmailAsync(
                    invitation.Email,
                    invitation.Token,
                    invitationUrl,
                    currentUser?.UserName ?? currentUser?.Email ?? "Administrator");

                if (!emailSent)
                {
                    _logger.LogWarning("Failed to resend invitation email to {Email}", invitation.Email);
                    return StatusCode(500, "Failed to send invitation email.");
                }

                return Ok("Invitation resent successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending invitation {Id}", id);
                return StatusCode(500, "An internal error occurred.");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInvitation(string id)
        {
            try
            {
                var invitation = await _context.Invitations.FindAsync(id);
                if (invitation == null)
                {
                    return NotFound("Invitation not found.");
                }

                _context.Invitations.Remove(invitation);
                await _context.SaveChangesAsync();

                return Ok("Invitation deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invitation {Id}", id);
                return StatusCode(500, "An internal error occurred.");
            }
        }

        // NEW: Create multiple invitations
        [HttpPost("multiple")]
        public async Task<ActionResult<BulkInvitationResponseDTO>> CreateMultipleInvitations([FromBody] CreateMultipleInvitationsDTO dto)
        {
            try
            {
                // Get current user
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Unauthorized();
                }

                var frontendBaseUrl = _configuration["FrontendBaseUrl"] ?? "https://1qtrdwgx-44369.uks1.devtunnels.ms";
                var invitationUrl = $"{frontendBaseUrl}/Account/Register";

                var results = new List<BulkInvitationResponseDTO>();
                var successfulInvitations = new List<InvitationResponseDTO>();
                var failedInvitations = new List<FailedInvitationDTO>();

                foreach (var email in dto.Emails)
                {
                    try
                    {
                        // Validate email format
                        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                        {
                            failedInvitations.Add(new FailedInvitationDTO
                            {
                                Email = email,
                                Error = "Invalid email format"
                            });
                            continue;
                        }

                        // Check if user already exists
                        var existingUser = await _userManager.FindByEmailAsync(email);
                        if (existingUser != null)
                        {
                            failedInvitations.Add(new FailedInvitationDTO
                            {
                                Email = email,
                                Error = "A user with this email already exists"
                            });
                            continue;
                        }

                        // Check for existing active invitation
                        var existingInvitation = await _context.Invitations
                            .FirstOrDefaultAsync(i => i.Email == email && !i.IsUsed && i.ExpiresAt > DateTime.UtcNow);

                        if (existingInvitation != null)
                        {
                            failedInvitations.Add(new FailedInvitationDTO
                            {
                                Email = email,
                                Error = "An active invitation already exists for this email"
                            });
                            continue;
                        }

                        // Create invitation
                        var invitation = new Invitation
                        {
                            Email = email,
                            Role = dto.Role,
                            InvitedByUserId = currentUser.Id,
                            CreatedAt = DateTime.UtcNow,
                            ExpiresAt = DateTime.UtcNow.AddDays(2)
                        };

                        _context.Invitations.Add(invitation);
                        await _context.SaveChangesAsync();

                        // Send invitation email
                        var emailSent = await _emailService.SendInvitationEmailAsync(
                            email,
                            invitation.Token,
                            invitationUrl,
                            currentUser.UserName ?? currentUser.Email ?? "Administrator");

                        if (!emailSent)
                        {
                            _logger.LogWarning("Failed to send invitation email to {Email}", email);
                            // Still count as successful but log the email failure
                        }

                        var invitationResponse = new InvitationResponseDTO
                        {
                            Id = invitation.Id,
                            Email = invitation.Email,
                            Token = invitation.Token,
                            CreatedAt = invitation.CreatedAt,
                            ExpiresAt = invitation.ExpiresAt,
                            IsUsed = invitation.IsUsed,
                            Role = invitation.Role,
                            InvitedByUserName = currentUser.UserName ?? currentUser.Email ?? "Administrator"
                        };

                        successfulInvitations.Add(invitationResponse);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating invitation for {Email}", email);
                        failedInvitations.Add(new FailedInvitationDTO
                        {
                            Email = email,
                            Error = "An internal error occurred"
                        });
                    }
                }

                var response = new BulkInvitationResponseDTO
                {
                    SuccessfulInvitations = successfulInvitations,
                    FailedInvitations = failedInvitations,
                    TotalProcessed = dto.Emails.Count,
                    SuccessfulCount = successfulInvitations.Count,
                    FailedCount = failedInvitations.Count
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating multiple invitations");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        // ADD THIS METHOD TO YOUR INVITATIONS CONTROLLER
        [HttpGet("validate/{token}")]
        [AllowAnonymous]
        public async Task<ActionResult<InvitationValidationResult>> ValidateInvitation(string token)
        {
            try
            {
                _logger.LogInformation("Validating invitation token: {Token}", token);

                // Validate token format
                if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(token, out _))
                {
                    _logger.LogWarning("Invalid token format: {Token}", token);
                    return BadRequest(new InvitationValidationResult
                    {
                        Success = false,
                        Message = "Invalid invitation token format."
                    });
                }

                var invitation = await _context.Invitations
                    .Include(i => i.InvitedByUser)
                    .FirstOrDefaultAsync(i => i.Token == token);

                if (invitation == null)
                {
                    return NotFound(new InvitationValidationResult
                    {
                        Success = false,
                        Message = "Invalid invitation token."
                    });
                }

                if (invitation.IsUsed)
                {
                    return BadRequest(new InvitationValidationResult
                    {
                        Success = false,
                        Message = "This invitation has already been used."
                    });
                }

                if (invitation.ExpiresAt < DateTime.UtcNow)
                {
                    return BadRequest(new InvitationValidationResult
                    {
                        Success = false,
                        Message = "This invitation has expired."
                    });
                }

                // Safely get the invited by user name
                string invitedByUserName = "Unknown";
                if (invitation.InvitedByUser != null)
                {
                    invitedByUserName = invitation.InvitedByUser.UserName ??
                                       invitation.InvitedByUser.Email ??
                                       "Unknown";
                }

                var result = new InvitationValidationResult
                {
                    Success = true,
                    Data = new InvitationResponseDTO
                    {
                        Id = invitation.Id,
                        Email = invitation.Email,
                        Token = invitation.Token,
                        CreatedAt = invitation.CreatedAt,
                        ExpiresAt = invitation.ExpiresAt,
                        IsUsed = invitation.IsUsed,
                        Role = invitation.Role,
                        InvitedByUserName = invitedByUserName
                    }
                };

                _logger.LogInformation("Invitation validation successful for email: {Email}", invitation.Email);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating invitation token {Token}", token);
                return StatusCode(500, new InvitationValidationResult
                {
                    Success = false,
                    Message = "An internal error occurred while validating the invitation."
                });
            }
        }

        
    }
}