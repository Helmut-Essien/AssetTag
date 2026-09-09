using AssetTag.Data;
using Shared.Models;
using Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace AssetTag.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = RoleNames.Admin)]
public class DepartmentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DepartmentsController(ApplicationDbContext context) => _context = context;

    // GET: /api/departments
    // Omit page/pageSize to return the full list (dropdowns).
    // Pass page (and optional pageSize) for PaginatedResponse.
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        var query = _context.Departments
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentReadDTO(d.DepartmentId, d.Name, d.Description));

        if (!page.HasValue)
            return Ok(await query.ToListAsync());

        var (normalizedPage, normalizedSize) = PaginatedResponse<DepartmentReadDTO>.Normalize(
            page.Value,
            pageSize ?? AssetConstants.Pagination.DefaultPageSize,
            AssetConstants.Pagination.DefaultPageSize,
            AssetConstants.Pagination.MaxPageSize);

        var totalCount = await query.CountAsync();
        var list = await query
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync();

        return Ok(PaginatedResponse<DepartmentReadDTO>.Create(list, totalCount, normalizedPage, normalizedSize));
    }

    // GET: /api/departments/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<DepartmentReadDTO>> Get(string id)
    {
        var dep = await _context.Departments.FindAsync(id);
        return dep is null ? NotFound() : Ok(new DepartmentReadDTO(dep.DepartmentId, dep.Name, dep.Description));
    }

    // POST: /api/departments/create
    [HttpPost]
    public async Task<ActionResult<DepartmentReadDTO>> Post([FromBody] DepartmentCreateDTO dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _context.Departments.AnyAsync(d => d.Name == dto.Name))
            return Conflict("Department name already exists.");

        var dep = new Department
        {
            Name = dto.Name,
            Description = dto.Description,
            DateModified = DateTime.UtcNow,
            // satisfy required navigation property
            Users = new List<ApplicationUser>()
        };

        _context.Departments.Add(dep);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = dep.DepartmentId },
            new DepartmentReadDTO(dep.DepartmentId, dep.Name, dep.Description));
    }

    // PUT: /api/departments/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Put(string id, [FromBody] DepartmentUpdateDTO dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (id != dto.DepartmentId) return BadRequest();

        var dep = await _context.Departments.FindAsync(id);
        if (dep is null) return NotFound();

        if (dto.Name is not null && dto.Name != dep.Name &&
            await _context.Departments.AnyAsync(d => d.Name == dto.Name))
            return Conflict("Department name already exists.");

        dep.Name = dto.Name ?? dep.Name;
        dep.Description = dto.Description;
        dep.DateModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: /api/departments/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var dep = await _context.Departments.FindAsync(id);
        if (dep is null) return NotFound();

        // Check if any assets or users reference this department
        if (await _context.Assets.AnyAsync(a => a.DepartmentId == id))
        {
            return BadRequest("Cannot delete this department because it is still assigned to one or more assets. Reassign or remove the assets first.");
        }

        if (await _context.Users.AnyAsync(u => u.DepartmentId == id))
        {
            return BadRequest("Cannot delete this department because it is still assigned to one or more users. Reassign or remove the users first.");
        }

        _context.Departments.Remove(dep);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
