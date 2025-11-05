using AssetTag.Data;
using AssetTag.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    [Authorize]  // Base authorization; endpoints can override
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            ILogger<UsersController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]  // Override to allow unauth if needed; adjust per security needs
        public async Task<ActionResult<IEnumerable<UserReadDTO>>> GetAllUsers(
            [FromQuery] string? search = null,
            [FromQuery] string? departmentId = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1 || pageSize < 1)
                {
                    return BadRequest("Invalid pagination parameters.");
                }

                var query = _context.Users.AsNoTracking().AsQueryable();

                // Search by name or email
                if (!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    query = query.Where(u =>
                        (u.FirstName != null && u.FirstName.ToLower().Contains(search)) ||
                        (u.Surname != null && u.Surname.ToLower().Contains(search)) ||
                        (u.Email != null && u.Email.ToLower().Contains(search)));
                }

                // Filter by department
                if (!string.IsNullOrEmpty(departmentId))
                {
                    query = query.Where(u => u.DepartmentId == departmentId);
                }

                // Filter by status
                if (isActive.HasValue)
                {
                    query = query.Where(u => u.IsActive == isActive.Value);
                }

                // Pagination
                var totalCount = await query.CountAsync();
                var users = await query
                    .OrderBy(u => u.UserName)  // Default sort; can extend
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new UserReadDTO
                    {
                        Id = u.Id,
                        UserName = u.UserName ?? string.Empty,
                        Email = u.Email ?? string.Empty,
                        FirstName = u.FirstName,
                        Surname = u.Surname,
                        OtherNames = u.OtherNames,
                        DateOfBirth = u.DateOfBirth,
                        Address = u.Address,
                        JobRole = u.JobRole,
                        ProfileImage = u.ProfileImage,
                        IsActive = u.IsActive,
                        // DateCreated is non-nullable on ApplicationUser; use a conditional fallback instead of ??
                        DateCreated = u.DateCreated == default ? DateTime.UtcNow : u.DateCreated,
                        DepartmentId = u.DepartmentId
                    })
                    .ToListAsync();

                // add pagination headers (best practice) — use the indexer to set/overwrite
                Response.Headers["X-Total-Count"] = totalCount.ToString();
                Response.Headers["X-Page"] = page.ToString();
                Response.Headers["X-Page-Size"] = pageSize.ToString();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all users.");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserReadDTO>> GetUserById(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                return Ok(MapToUserReadDTO(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching user by ID: {id}");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        [HttpGet("by-email/{email}")]
        public async Task<ActionResult<UserReadDTO>> GetUserByEmail(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    return NotFound($"User with email '{email}' not found.");
                }

                return Ok(MapToUserReadDTO(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching user by email: {email}");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        // Helper method for mapping
        private UserReadDTO MapToUserReadDTO(ApplicationUser user)
        {
            return new UserReadDTO
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                Surname = user.Surname,
                OtherNames = user.OtherNames,
                DateOfBirth = user.DateOfBirth,
                Address = user.Address,
                JobRole = user.JobRole,
                ProfileImage = user.ProfileImage,
                IsActive = user.IsActive,
                // Match the projection above: fall back to UtcNow if DateCreated is default
                DateCreated = user.DateCreated == default ? DateTime.UtcNow : user.DateCreated,
                DepartmentId = user.DepartmentId
            };
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]  // Admin-only
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UserUpdateDTO dto)
        {
            if (id != dto.Id)
            {
                return BadRequest("ID mismatch.");
            }

            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                // Partial update
                if (!string.IsNullOrEmpty(dto.FirstName)) user.FirstName = dto.FirstName;
                if (!string.IsNullOrEmpty(dto.Surname)) user.Surname = dto.Surname;
                if (!string.IsNullOrEmpty(dto.OtherNames)) user.OtherNames = dto.OtherNames;
                if (dto.DateOfBirth.HasValue) user.DateOfBirth = dto.DateOfBirth;
                if (!string.IsNullOrEmpty(dto.Address)) user.Address = dto.Address;
                if (!string.IsNullOrEmpty(dto.JobRole)) user.JobRole = dto.JobRole;
                if (!string.IsNullOrEmpty(dto.ProfileImage)) user.ProfileImage = dto.ProfileImage;
                if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;
                if (!string.IsNullOrEmpty(dto.DepartmentId)) user.DepartmentId = dto.DepartmentId;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    _logger.LogWarning("Update failed for user {Id}: {Errors}", id, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return BadRequest(result.Errors);
                }

                return Ok($"User '{user.UserName}' updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user {id}");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        [HttpPatch("{id}/activation")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleActivation(string id, [FromBody] bool isActive)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                user.IsActive = isActive;
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }

                return Ok($"User '{user.UserName}' activation set to {isActive}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error toggling activation for user {id}");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SoftDeleteUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                user.IsActive = false;
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }

                return Ok($"User '{user.UserName}' deactivated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error soft-deleting user {id}");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        [HttpGet("{id}/roles")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<string>>> GetUserRoles(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                var roles = await _userManager.GetRolesAsync(user);
                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching roles for user {id}");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        [HttpPost("{id}/roles")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddUserToRole(string id, [FromBody] AssignRoleDTO dto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                if (!await _roleManager.RoleExistsAsync(dto.RoleName))
                {
                    return BadRequest($"Role '{dto.RoleName}' does not exist.");
                }

                var result = await _userManager.AddToRoleAsync(user, dto.RoleName);
                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }

                return Ok($"Role '{dto.RoleName}' added to user '{user.UserName}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding role for user {id}");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        [HttpDelete("{id}/roles")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveUserFromRole(string id, [FromBody] AssignRoleDTO dto)  // Reuse DTO for roleName
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                if (!await _userManager.IsInRoleAsync(user, dto.RoleName))
                {
                    return BadRequest($"User '{user.UserName}' is not in role '{dto.RoleName}'.");
                }

                var result = await _userManager.RemoveFromRoleAsync(user, dto.RoleName);
                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }

                return Ok($"Role '{dto.RoleName}' removed from user '{user.UserName}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing role for user {id}");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        [HttpPost("{id}/password-reset")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<string>> ResetUserPassword(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                // Note: Client should email this token to the user

                return Ok(resetToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating password reset for user {id}");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        // Action methods will follow...
    }
}