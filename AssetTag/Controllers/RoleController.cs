using Shared.Models;
using Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace AssetTag.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = RoleNames.Admin)]
    public class RoleController : ControllerBase
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public RoleController(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllRoles()
        {
            var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            return Ok(roles);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RoleName))
            {
                return BadRequest("Role name is required.");
            }

            if (await _roleManager.RoleExistsAsync(dto.RoleName))
            {
                return BadRequest($"Role '{dto.RoleName}' already exists.");
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(dto.RoleName));
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }
            return Ok($"Role '{dto.RoleName}' created successfully.");
        }

        [HttpDelete("{roleName}")]
        public async Task<IActionResult> DeleteRole(string roleName)
        {
            if (RoleNames.IsBuiltIn(roleName))
            {
                return BadRequest($"Cannot delete built-in role '{roleName}'.");
            }

            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                return NotFound($"Role '{roleName}' not found.");
            }

            var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
            if (usersInRole.Any())
            {
                return BadRequest($"Cannot delete role '{roleName}' because it is assigned to users.");
            }

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }
            return Ok($"Role '{roleName}' deleted successfully.");
        }

        [HttpPost("Assign")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleByEmailDTO dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                return NotFound($"User with email '{dto.Email}' not found.");
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

            var stampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                return StatusCode(500, "Role was assigned but session invalidation failed. Ask the user to sign in again.");
            }

            return Ok($"Role '{dto.RoleName}' assigned to user '{dto.Email}' successfully.");
        }
    }
}
