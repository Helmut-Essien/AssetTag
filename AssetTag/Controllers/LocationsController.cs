using AssetTag.Data;
using AssetTag.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace AssetTag.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class LocationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public LocationsController(ApplicationDbContext context) => _context = context;

    // GET: /api/locations
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LocationReadDTO>>> GetAll()
    {
        var list = await _context.Locations
            .AsNoTracking()
            .Select(l => new LocationReadDTO(
                l.LocationId,
                l.Name,
                l.Description,
                l.Campus,
                l.Building,
                l.Room,
                l.Latitude,
                l.Longitude))
            .ToListAsync();

        return Ok(list);
    }

    // GET: /api/locations/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<LocationReadDTO>> Get(string id)
    {
        var loc = await _context.Locations.FindAsync(id);
        if (loc is null) return NotFound();

        var dto = new LocationReadDTO(
            loc.LocationId,
            loc.Name,
            loc.Description,
            loc.Campus,
            loc.Building,
            loc.Room,
            loc.Latitude,
            loc.Longitude);

        return Ok(dto);
    }

    // POST: /api/locations
    [HttpPost]
    public async Task<ActionResult<LocationReadDTO>> Create([FromBody] LocationCreateDTO dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // enforce unique Name+Campus
        if (await _context.Locations.AnyAsync(l => l.Name == dto.Name && l.Campus == dto.Campus))
            return Conflict("A location with the same name and campus already exists.");

        var loc = new Location
        {
            Name = dto.Name,
            Description = dto.Description,
            Campus = dto.Campus,
            Building = dto.Building,
            Room = dto.Room,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Assets = new List<Asset>() // satisfy required navigation property
        };

        _context.Locations.Add(loc);
        await _context.SaveChangesAsync();

        var read = new LocationReadDTO(loc.LocationId, loc.Name, loc.Description, loc.Campus, loc.Building, loc.Room, loc.Latitude, loc.Longitude);
        return CreatedAtAction(nameof(Get), new { id = loc.LocationId }, read);
    }

    // PUT: /api/locations/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] LocationUpdateDTO dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var loc = await _context.Locations.FindAsync(id);
        if (loc is null) return NotFound();

        // compute new name/campus for uniqueness check
        var newName = dto.Name ?? loc.Name;
        var newCampus = dto.Campus ?? loc.Campus;

        if ((newName != loc.Name || newCampus != loc.Campus) &&
            await _context.Locations.AnyAsync(l => l.LocationId != id && l.Name == newName && l.Campus == newCampus))
        {
            return Conflict("A location with the same name and campus already exists.");
        }

        loc.Name = dto.Name ?? loc.Name;
        loc.Description = dto.Description ?? loc.Description;
        loc.Campus = dto.Campus ?? loc.Campus;
        loc.Building = dto.Building ?? loc.Building;
        loc.Room = dto.Room ?? loc.Room;
        loc.Latitude = dto.Latitude ?? loc.Latitude;
        loc.Longitude = dto.Longitude ?? loc.Longitude;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: /api/locations/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var loc = await _context.Locations.FindAsync(id);
        if (loc is null) return NotFound();

        _context.Locations.Remove(loc);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}