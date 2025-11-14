using System;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs;

public record AssetHistoryCreateDTO
{
    [Required]
    public string AssetId { get; init; } = string.Empty;

    [Required]
    public string UserId { get; init; } = string.Empty;

    [Required]
    public string Action { get; init; } = string.Empty;

    [Required]
    public string Description { get; init; } = string.Empty;

    public string? OldLocationId { get; init; }
    public string? NewLocationId { get; init; }
    public string? OldStatus { get; init; }
    public string? NewStatus { get; init; }
}

public record AssetHistoryReadDTO
{
    public string HistoryId { get; init; } = string.Empty;
    public string AssetId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string? OldLocationId { get; init; }
    public string? NewLocationId { get; init; }
    public string? OldStatus { get; init; }
    public string? NewStatus { get; init; }

    public string? AssetName { get; init; }
    public string? UserFullName { get; init; }
    public string? OldLocationName { get; init; }
    public string? NewLocationName { get; init; }
}

public class PaginatedResponse<T>
{
    public List<T> Data { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
}

public class AssetHistoryFilters
{
    public List<string> Actions { get; set; } = new();
    public Dictionary<string, string> RecentAssets { get; set; } = new();
    public DateRangeFilter DateRange { get; set; } = new();
}

public class DateRangeFilter
{
    public DateTime? MinDate { get; set; }
    public DateTime? MaxDate { get; set; }
}