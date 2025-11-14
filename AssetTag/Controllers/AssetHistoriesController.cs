using AssetTag.Data;
using AssetTag.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace AssetTag.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AssetHistoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AssetHistoriesController(ApplicationDbContext context) => _context = context;

    // GET: /api/assethistories
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AssetHistoryReadDTO>>> Get()
    {
        return Ok(await _context.AssetHistories
            .AsNoTracking()
            .Include(h => h.Asset)
            .Include(h => h.User)
            .Include(h => h.OldLocation)
            .Include(h => h.NewLocation)
            .Select(h => new AssetHistoryReadDTO
            {
                HistoryId = h.HistoryId,
                AssetId = h.AssetId,
                UserId = h.UserId,
                Action = h.Action,
                Description = h.Description,
                Timestamp = h.Timestamp,
                OldLocationId = h.OldLocationId,
                NewLocationId = h.NewLocationId,
                OldStatus = h.OldStatus,
                NewStatus = h.NewStatus,
                AssetName = h.Asset.Name,
                UserFullName = string.IsNullOrWhiteSpace(h.User.OtherNames)
                    ? $"{h.User.FirstName} {h.User.Surname}"
                    : $"{h.User.FirstName} {h.User.OtherNames} {h.User.Surname}",
                OldLocationName = h.OldLocation != null ? h.OldLocation.Name : null,
                NewLocationName = h.NewLocation != null ? h.NewLocation.Name : null
            })
            .OrderByDescending(h => h.Timestamp)
            .ToListAsync());
    }

    // GET: /api/assethistories/asset/{assetId}
    [HttpGet("asset/{assetId}")]
    public async Task<ActionResult<IEnumerable<AssetHistoryReadDTO>>> GetByAssetId(string assetId)
    {
        var histories = await _context.AssetHistories
            .AsNoTracking()
            .Include(h => h.Asset)
            .Include(h => h.User)
            .Include(h => h.OldLocation)
            .Include(h => h.NewLocation)
            .Where(h => h.AssetId == assetId)
            .Select(h => new AssetHistoryReadDTO
            {
                HistoryId = h.HistoryId,
                AssetId = h.AssetId,
                UserId = h.UserId,
                Action = h.Action,
                Description = h.Description,
                Timestamp = h.Timestamp,
                OldLocationId = h.OldLocationId,
                NewLocationId = h.NewLocationId,
                OldStatus = h.OldStatus,
                NewStatus = h.NewStatus,
                AssetName = h.Asset.Name,
                UserFullName = string.IsNullOrWhiteSpace(h.User.OtherNames)
                    ? $"{h.User.FirstName} {h.User.Surname}"
                    : $"{h.User.FirstName} {h.User.OtherNames} {h.User.Surname}",
                OldLocationName = h.OldLocation != null ? h.OldLocation.Name : null,
                NewLocationName = h.NewLocation != null ? h.NewLocation.Name : null
            })
            .OrderByDescending(h => h.Timestamp)
            .ToListAsync();

        return Ok(histories);
    }

    // GET: /api/assethistories/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<AssetHistoryReadDTO>> Get(string id)
    {
        var history = await _context.AssetHistories
            .Include(h => h.Asset)
            .Include(h => h.User)
            .Include(h => h.OldLocation)
            .Include(h => h.NewLocation)
            .FirstOrDefaultAsync(h => h.HistoryId == id);

        if (history is null) return NotFound();

        return Ok(new AssetHistoryReadDTO
        {
            HistoryId = history.HistoryId,
            AssetId = history.AssetId,
            UserId = history.UserId,
            Action = history.Action,
            Description = history.Description,
            Timestamp = history.Timestamp,
            OldLocationId = history.OldLocationId,
            NewLocationId = history.NewLocationId,
            OldStatus = history.OldStatus,
            NewStatus = history.NewStatus,
            AssetName = history.Asset.Name,
            UserFullName = GetUserFullName(history.User),
            OldLocationName = history.OldLocation?.Name,
            NewLocationName = history.NewLocation?.Name
        });
    }

    // POST: /api/assethistories
    [HttpPost]
    public async Task<ActionResult<AssetHistoryReadDTO>> Post([FromBody] AssetHistoryCreateDTO dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Verify asset exists
        var asset = await _context.Assets.FindAsync(dto.AssetId);
        if (asset is null) return BadRequest("Asset not found");

        // Verify user exists
        var user = await _context.Users.FindAsync(dto.UserId);
        if (user is null) return BadRequest("User not found");

        var history = new AssetHistory
        {
            AssetId = dto.AssetId,
            UserId = dto.UserId,
            Action = dto.Action,
            Description = dto.Description,
            OldLocationId = dto.OldLocationId,
            NewLocationId = dto.NewLocationId,
            OldStatus = dto.OldStatus,
            NewStatus = dto.NewStatus,
            Asset = asset,
            User = user
        };

        _context.AssetHistories.Add(history);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = history.HistoryId },
            new AssetHistoryReadDTO
            {
                HistoryId = history.HistoryId,
                AssetId = history.AssetId,
                UserId = history.UserId,
                Action = history.Action,
                Description = history.Description,
                Timestamp = history.Timestamp,
                OldLocationId = history.OldLocationId,
                NewLocationId = history.NewLocationId,
                OldStatus = history.OldStatus,
                NewStatus = history.NewStatus,
                UserFullName = GetUserFullName(user)
            });
    }

    // Helper method to format user's full name
    private static string GetUserFullName(ApplicationUser user)
    {
        var fullName = $"{user.FirstName} {user.Surname}";

        if (!string.IsNullOrWhiteSpace(user.OtherNames))
        {
            fullName = $"{user.FirstName} {user.OtherNames} {user.Surname}";
        }

        return fullName.Trim();
    }
}