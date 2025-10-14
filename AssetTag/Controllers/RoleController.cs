using AssetTag.DTOs;
using AssetTag.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AssetTag.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoleController : ControllerBase
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public RoleController(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }

        [HttpPost("Create")]
        public async Task<IActionResult> CreateRole([FromBody] string roleName)
        {
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                return BadRequest($"Role '{roleName}'already exists.");
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }
            return Ok($"Role '{roleName}' created successfully.");
        }

        [HttpPost("Assign")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleDTO dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                return NotFound($"User with email '{dto.Email}' not found.");
            }

            var result = await _userManager.AddToRoleAsync(user, dto.RoleName);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok($"Role '{dto.RoleName}' assigned to user '{dto.Email}' successfully.");

        }
    }
}
