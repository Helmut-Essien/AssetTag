using System;
using System.Collections.Generic;

namespace Shared.DTOs;

/// <summary>
/// Request to push offline changes from mobile to server
/// </summary>
public record SyncPushRequestDTO
{
    public List<SyncOperationDTO> Operations { get; init; } = new();
    public string DeviceId { get; init; } = string.Empty;
}

/// <summary>
/// Individual sync operation (CREATE, UPDATE, DELETE)
/// </summary>
public record SyncOperationDTO
{
    public string EntityType { get; init; } = string.Empty; // "Asset", "AssetHistory"
    public string EntityId { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty; // "CREATE", "UPDATE", "DELETE"
    public string JsonData { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Response from push operation
/// </summary>
public record SyncPushResponseDTO
{
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public List<SyncErrorDTO> Errors { get; init; } = new();
}

/// <summary>
/// Sync error details
/// </summary>
public record SyncErrorDTO
{
    public string EntityId { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

/// <summary>
/// Request to pull changes from server
/// </summary>
public record SyncPullRequestDTO
{
    public DateTime? LastSyncTimestamp { get; init; }
    public string DeviceId { get; init; } = string.Empty;
}

/// <summary>
/// Response with delta changes from server
/// </summary>
public record SyncPullResponseDTO
{
    public List<AssetReadDTO> Assets { get; init; } = new();
    public List<CategoryReadDTO> Categories { get; init; } = new();
    public List<LocationReadDTO> Locations { get; init; } = new();
    public List<DepartmentReadDTO> Departments { get; init; } = new();
    public DateTime ServerTimestamp { get; init; }
}