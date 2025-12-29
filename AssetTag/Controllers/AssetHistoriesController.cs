using AssetTag.Data;
using Shared.Models;
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
    public async Task<ActionResult<PaginatedResponse<AssetHistoryReadDTO>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? action = null,
        [FromQuery] string? assetName = null,
        [FromQuery] string? userName = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.AssetHistories
            .AsNoTracking()
            .Include(h => h.Asset)
            .Include(h => h.User)
            .Include(h => h.OldLocation)
            .Include(h => h.NewLocation)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(action))
            query = query.Where(h => h.Action == action);

        if (!string.IsNullOrEmpty(assetName))
            query = query.Where(h => h.Asset.Name.Contains(assetName));

        if (!string.IsNullOrEmpty(userName))
            query = query.Where(h =>
                (h.User.FirstName + " " + h.User.Surname).Contains(userName) ||
                (h.User.FirstName + " " + h.User.OtherNames + " " + h.User.Surname).Contains(userName));

        if (fromDate.HasValue)
            query = query.Where(h => h.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(h => h.Timestamp <= toDate.Value.AddDays(1).AddTicks(-1)); // Include entire day

        // Get total count
        var totalCount = await query.CountAsync();

        // Apply pagination and select
        var histories = await query
            .OrderByDescending(h => h.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new PaginatedResponse<AssetHistoryReadDTO>
        {
            Data = histories,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasPrevious = page > 1,
            HasNext = page < totalPages
        });
    }

    // GET: /api/assethistories/asset/{assetId}
    [HttpGet("asset/{assetId}")]
    public async Task<ActionResult<PaginatedResponse<AssetHistoryReadDTO>>> GetByAssetId(
        string assetId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.AssetHistories
            .AsNoTracking()
            .Include(h => h.Asset)
            .Include(h => h.User)
            .Include(h => h.OldLocation)
            .Include(h => h.NewLocation)
            .Where(h => h.AssetId == assetId);

        // Apply filters
        if (!string.IsNullOrEmpty(action))
            query = query.Where(h => h.Action == action);

        if (fromDate.HasValue)
            query = query.Where(h => h.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(h => h.Timestamp <= toDate.Value.AddDays(1).AddTicks(-1));

        // Get total count
        var totalCount = await query.CountAsync();

        // Apply pagination and select
        var histories = await query
            .OrderByDescending(h => h.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new PaginatedResponse<AssetHistoryReadDTO>
        {
            Data = histories,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasPrevious = page > 1,
            HasNext = page < totalPages
        });
    }

    // GET: /api/assethistories/search
    [HttpGet("search")]
    public async Task<ActionResult<PaginatedResponse<AssetHistoryReadDTO>>> Search(
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        if (string.IsNullOrWhiteSpace(query))
        {
            return await Get(page, pageSize);
        }

        var searchQuery = _context.AssetHistories
            .AsNoTracking()
            .Include(h => h.Asset)
            .Include(h => h.User)
            .Include(h => h.OldLocation)
            .Include(h => h.NewLocation)
            .AsQueryable();

        // Apply search across multiple fields
        searchQuery = searchQuery.Where(h =>
            h.Action.Contains(query) ||
            h.Description.Contains(query) ||
            h.Asset.Name.Contains(query) ||
            (h.User.FirstName + " " + h.User.Surname).Contains(query) ||
            (h.User.FirstName + " " + h.User.OtherNames + " " + h.User.Surname).Contains(query) ||
            (h.OldLocation != null && h.OldLocation.Name.Contains(query)) ||
            (h.NewLocation != null && h.NewLocation.Name.Contains(query)) ||
            (h.OldStatus != null && h.OldStatus.Contains(query)) ||
            (h.NewStatus != null && h.NewStatus.Contains(query)));

        // Get total count
        var totalCount = await searchQuery.CountAsync();

        // Apply pagination and select
        var histories = await searchQuery
            .OrderByDescending(h => h.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new PaginatedResponse<AssetHistoryReadDTO>
        {
            Data = histories,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasPrevious = page > 1,
            HasNext = page < totalPages
        });
    }

    // GET: /api/assethistories/filters
    [HttpGet("filters")]
    public async Task<ActionResult<AssetHistoryFilters>> GetFilters()
    {
        var actions = await _context.AssetHistories
            .AsNoTracking()
            .Select(h => h.Action)
            .Distinct()
            .ToListAsync();

        var recentAssets = await _context.AssetHistories
            .AsNoTracking()
            .Include(h => h.Asset)
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new { h.AssetId, h.Asset.Name })
            .Distinct()
            .Take(10)
            .ToListAsync();

        var dateRange = await _context.AssetHistories
            .AsNoTracking()
            .Select(h => h.Timestamp) // Fixed typo: was Testamp, now Timestamp
            .OrderBy(t => t)
            .ToListAsync();

        var minDate = dateRange.FirstOrDefault();
        var maxDate = dateRange.LastOrDefault();

        return Ok(new AssetHistoryFilters
        {
            Actions = actions,
            RecentAssets = recentAssets.ToDictionary(x => x.AssetId, x => x.Name),
            DateRange = new DateRangeFilter
            {
                MinDate = minDate,
                MaxDate = maxDate
            }
        });
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

