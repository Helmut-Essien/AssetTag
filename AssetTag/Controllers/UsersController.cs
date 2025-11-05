using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using AssetTag.Models;
using AssetTag.Services;
using System.ComponentModel.DataAnnotations;

namespace AssetTag.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            ITokenService tokenService,
            ILogger<UsersController> logger)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _logger = logger;
        }

        // GET: api/users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserReadDTO>>> GetUsers(
         [FromQuery] string? search = null,
         [FromQuery] string? departmentId = null,
         [FromQuery] bool? isActive = null,
         [FromQuery] int page = 1,
         [FromQuery] int pageSize = 20)
        {
            try
            {
                // Null check (defensive programming)
                if (_userManager.Users == null)
                {
                    return StatusCode(500, "Unable to access users collection");
                }

                var query = _userManager.Users.AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(u =>
                        u.FirstName.Contains(search) ||
                        u.Surname.Contains(search) ||
                        u.Email.Contains(search) ||
                        u.UserName.Contains(search));
                }

                if (!string.IsNullOrEmpty(departmentId))
                {
                    query = query.Where(u => u.DepartmentId == departmentId);
                }

                if (isActive.HasValue)
                {
                    query = query.Where(u => u.IsActive == isActive.Value);
                }

                // Pagination
                var totalCount = await query.CountAsync();
                var users = await query
                    .OrderBy(u => u.Surname)
                    .ThenBy(u => u.FirstName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new UserReadDTO
                    {
                        Id = u.Id,
                        UserName = u.UserName,
                        Email = u.Email,
                        FirstName = u.FirstName,
                        Surname = u.Surname,
                        OtherNames = u.OtherNames,
                        DateOfBirth = u.DateOfBirth,
                        Address = u.Address,
                        JobRole = u.JobRole,
                        ProfileImage = u.ProfileImage,
                        IsActive = u.IsActive,
                        DateCreated = u.DateCreated,
                        DepartmentId = u.DepartmentId
                    })
                    .ToListAsync();

                // HIGH PERFORMANCE: Use indexer for headers
                Response.Headers["X-Total-Count"] = totalCount.ToString();
                Response.Headers["X-Page"] = page.ToString();
                Response.Headers["X-Page-Size"] = pageSize.ToString();
                Response.Headers["X-Total-Pages"] = Math.Ceiling((double)totalCount / pageSize).ToString();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, "An error occurred while retrieving users");
            }
        }

        // GET: api/users/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<UserReadDTO>> GetUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                var userDto = MapToUserReadDTO(user);
                return Ok(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with ID: {UserId}", id);
                return StatusCode(500, "An error occurred while retrieving the user");
            }
        }

        // GET: api/users/by-email/{email}
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

                var userDto = MapToUserReadDTO(user);
                return Ok(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with email: {Email}", email);
                return StatusCode(500, "An error occurred while retrieving the user");
            }
        }

        // PUT: api/users/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(string id, UserUpdateDTO userUpdateDto)
        {
            try
            {
                if (id != userUpdateDto.Id)
                {
                    return BadRequest("User ID in route does not match ID in request body");
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                // Update user properties
                UpdateUserFromDTO(user, userUpdateDto);

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }

                var updatedUserDto = MapToUserReadDTO(user);
                return Ok(updatedUserDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user with ID: {UserId}", id);
                return StatusCode(500, "An error occurred while updating the user");
            }
        }

        // PATCH: api/users/{id}/activation
        [HttpPatch("{id}/activation")]
        public async Task<IActionResult> UpdateUserActivation(string id, [FromBody] UserActivationDTO activationDto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                user.IsActive = activationDto.IsActive;
                var result = await _userManager.UpdateAsync(user);

                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }

                return Ok(new { Message = $"User {(activationDto.IsActive ? "activated" : "deactivated")} successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating activation for user with ID: {UserId}", id);
                return StatusCode(500, "An error occurred while updating user activation");
            }
        }

        // DELETE: api/users/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                // Soft delete by deactivating
                user.IsActive = false;
                var result = await _userManager.UpdateAsync(user);

                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }

                return Ok(new { Message = "User deactivated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user with ID: {UserId}", id);
                return StatusCode(500, "An error occurred while deleting the user");
            }
        }

        // GET: api/users/{id}/roles
        [HttpGet("{id}/roles")]
        public async Task<ActionResult<UserRolesDTO>> GetUserRoles(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                var roles = await _userManager.GetRolesAsync(user);
                var userRolesDto = new UserRolesDTO
                {
                    UserId = user.Id,
                    Email = user.Email,
                    UserName = user.UserName,
                    Roles = roles.ToList()
                };

                return Ok(userRolesDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles for user with ID: {UserId}", id);
                return StatusCode(500, "An error occurred while retrieving user roles");
            }
        }

        // POST: api/users/{id}/roles
        [HttpPost("{id}/roles")]
        public async Task<IActionResult> AddUserToRole(string id, [FromBody] AddUserToRoleDTO addToRoleDto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                var result = await _userManager.AddToRoleAsync(user, addToRoleDto.RoleName);
                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }

                return Ok(new { Message = $"User added to role '{addToRoleDto.RoleName}' successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user to role. User ID: {UserId}, Role: {Role}", id, addToRoleDto.RoleName);
                return StatusCode(500, "An error occurred while adding user to role");
            }
        }

        // DELETE: api/users/{id}/roles
        [HttpDelete("{id}/roles")]
        public async Task<IActionResult> RemoveUserFromRole(string id, [FromBody] RemoveUserFromRoleDTO removeFromRoleDto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                var result = await _userManager.RemoveFromRoleAsync(user, removeFromRoleDto.RoleName);
                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }

                return Ok(new { Message = $"User removed from role '{removeFromRoleDto.RoleName}' successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user from role. User ID: {UserId}, Role: {Role}", id, removeFromRoleDto.RoleName);
                return StatusCode(500, "An error occurred while removing user from role");
            }
        }

        // POST: api/users/{id}/password-reset
        [HttpPost("{id}/password-reset")]
        public async Task<IActionResult> ResetPassword(string id, [FromBody] ResetPasswordDTO resetPasswordDto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID '{id}' not found.");
                }

                // Generate password reset token and reset password
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, resetPasswordDto.NewPassword);

                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }

                return Ok(new { Message = "Password reset successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for user with ID: {UserId}", id);
                return StatusCode(500, "An error occurred while resetting password");
            }
        }

        private UserReadDTO MapToUserReadDTO(ApplicationUser user)
        {
            return new UserReadDTO
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                FirstName = user.FirstName,
                Surname = user.Surname,
                OtherNames = user.OtherNames,
                DateOfBirth = user.DateOfBirth,
                Address = user.Address,
                JobRole = user.JobRole,
                ProfileImage = user.ProfileImage,
                IsActive = user.IsActive,
                DateCreated = user.DateCreated,
                DepartmentId = user.DepartmentId
            };
        }

        private void UpdateUserFromDTO(ApplicationUser user, UserUpdateDTO dto)
        {
            if (!string.IsNullOrEmpty(dto.FirstName)) user.FirstName = dto.FirstName;
            if (!string.IsNullOrEmpty(dto.Surname)) user.Surname = dto.Surname;
            if (dto.OtherNames != null) user.OtherNames = dto.OtherNames;
            if (dto.DateOfBirth.HasValue) user.DateOfBirth = dto.DateOfBirth.Value;
            if (dto.Address != null) user.Address = dto.Address;
            if (dto.JobRole != null) user.JobRole = dto.JobRole;
            if (dto.ProfileImage != null) user.ProfileImage = dto.ProfileImage;
            if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;
            if (dto.DepartmentId != null) user.DepartmentId = dto.DepartmentId;
        }
    }

    // Additional DTOs
    public record UserActivationDTO
    {
        [Required]
        public bool IsActive { get; init; }
    }

    public record UserRolesDTO
    {
        public string UserId { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public List<string> Roles { get; init; } = new();
    }

    public record AddUserToRoleDTO
    {
        [Required]
        public string RoleName { get; init; } = string.Empty;
    }

    public record RemoveUserFromRoleDTO
    {
        [Required]
        public string RoleName { get; init; } = string.Empty;
    }

    public record ResetPasswordDTO
    {
        [Required]
        [MinLength(6)]
        public string NewPassword { get; init; } = string.Empty;
    }
}