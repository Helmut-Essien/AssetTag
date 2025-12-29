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
public class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryReadDTO>>> Get() =>
        Ok(await _context.Categories
            .AsNoTracking()
            .Select(c => new CategoryReadDTO(c.CategoryId, c.Name, c.Description))
            .ToListAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryReadDTO>> Get(string id)
    {
        var cat = await _context.Categories.FindAsync(id);
        return cat is null ? NotFound() : Ok(new CategoryReadDTO(cat.CategoryId, cat.Name, cat.Description));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryReadDTO>> Post(CategoryCreateDTO dto)
    {
        if (await _context.Categories.AnyAsync(c => c.Name == dto.Name))
            return Conflict("Category name already exists.");

        var cat = new Category { Name = dto.Name, Description = dto.Description };
        _context.Categories.Add(cat);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = cat.CategoryId },
            new CategoryReadDTO(cat.CategoryId, cat.Name, cat.Description));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(string id, CategoryUpdateDTO dto)
    {
        if (id != dto.CategoryId) return BadRequest();
        var cat = await _context.Categories.FindAsync(id);
        if (cat is null) return NotFound();

        if (dto.Name is not null && dto.Name != cat.Name &&
            await _context.Categories.AnyAsync(c => c.Name == dto.Name))
            return Conflict("Category name already exists.");

        cat.Name = dto.Name ?? cat.Name;
        cat.Description = dto.Description;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var cat = await _context.Categories.FindAsync(id);
        if (cat is null) return NotFound();
        _context.Categories.Remove(cat);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}