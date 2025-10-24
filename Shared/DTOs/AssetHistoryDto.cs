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
}