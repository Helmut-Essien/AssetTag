using AssetTag.Data;
using Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace AssetTag.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class DepartmentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DepartmentsController(ApplicationDbContext context) => _context = context;

    // GET: /api/departments
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DepartmentReadDTO>>> Get() =>
        Ok(await _context.Departments
            .AsNoTracking()
            .Select(d => new DepartmentReadDTO(d.DepartmentId, d.Name, d.Description))
            .ToListAsync());

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
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: /api/departments/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var dep = await _context.Departments.FindAsync(id);
        if (dep is null) return NotFound();

        _context.Departments.Remove(dep);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}