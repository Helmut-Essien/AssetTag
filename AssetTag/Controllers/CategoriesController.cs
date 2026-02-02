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
            .Select(c => new CategoryReadDTO(c.CategoryId, c.Name, c.Description, c.DepreciationRate))
            .ToListAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryReadDTO>> Get(string id)
    {
        var cat = await _context.Categories.FindAsync(id);
        return cat is null ? NotFound() : Ok(new CategoryReadDTO(cat.CategoryId, cat.Name, cat.Description, cat.DepreciationRate));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryReadDTO>> Post(CategoryCreateDTO dto)
    {
        if (await _context.Categories.AnyAsync(c => c.Name == dto.Name))
            return Conflict("Category name already exists.");

        var cat = new Category { Name = dto.Name, Description = dto.Description, DepreciationRate = dto.DepreciationRate };
        _context.Categories.Add(cat);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = cat.CategoryId },
            new CategoryReadDTO(cat.CategoryId, cat.Name, cat.Description, cat.DepreciationRate));
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
        cat.DepreciationRate = dto.DepreciationRate;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    //[HttpDelete("{id}")]
    //public async Task<IActionResult> Delete(string id)
    //{
    //    var cat = await _context.Categories.FindAsync(id);
    //    if (cat is null) return NotFound();
    //    _context.Categories.Remove(cat);
    //    await _context.SaveChangesAsync();
    //    return NoContent();
    //}

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var category = await _context.Categories
            .Include(c => c.Assets)  // Load related assets (or use .Any() below for efficiency)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category == null)
            return NotFound();

        // Option A: If you already Included the collection
        //if (category.Assets.Any())
        //{
        //    return BadRequest("Cannot delete this category because it is still assigned to one or more assets. Please reassign or remove the assets first.");
        //}

        // Option B: More efficient (no loading full list) — use this instead of Include if you prefer
        if (await _context.Assets.AnyAsync(a => a.CategoryId == id))
        {
            return BadRequest("Cannot delete this category because it is still assigned to one or more assets. Please reassign or remove the assets first.");
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}