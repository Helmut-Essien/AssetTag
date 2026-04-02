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
    /// <summary>
    /// Local queue item ID (used to track which operations succeeded)
    /// </summary>
    public int QueueItemId { get; init; }
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
    /// <summary>
    /// IDs of operations that were successfully processed (to be removed from mobile queue)
    /// </summary>
    public List<int> SuccessfulOperationIds { get; init; } = new();
    
    /// <summary>
    /// ENHANCEMENT #8: Sync metrics for this push operation
    /// </summary>
    public SyncMetrics? Metrics { get; init; }
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
/// Properties ordered by dependency: reference data first, then assets
/// </summary>
public record SyncPullResponseDTO
{
    // Reference data FIRST (assets depend on these)
    public List<CategoryReadDTO> Categories { get; init; } = new();
    public List<LocationReadDTO> Locations { get; init; } = new();
    public List<DepartmentReadDTO> Departments { get; init; } = new();
    
    // Assets LAST (depend on the above reference data)
    public List<AssetReadDTO> Assets { get; init; } = new();
    
    public DateTime ServerTimestamp { get; init; }
    
    // FIX #5: Deleted items tracking - mobile clients need to know what was deleted
    public List<DeletedItemDTO> DeletedItems { get; init; } = new();
    
    /// <summary>
    /// ENHANCEMENT #8: Sync metrics for this pull operation
    /// </summary>
    public SyncMetrics? Metrics { get; init; }
}

/// <summary>
/// FIX #5: Represents a deleted entity that mobile clients need to remove
/// </summary>
public record DeletedItemDTO(
    string EntityType,      // "Asset", "Category", "Location", "Department"
    string EntityId,        // ID of the deleted entity
    DateTime DeletedAt      // When it was deleted
);

/// <summary>
/// ENHANCEMENT #8: Sync operation metrics for monitoring and telemetry
/// </summary>
public record SyncMetrics
{
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public TimeSpan Duration => EndTime - StartTime;
    
    // Push metrics
    public int PushOperationsAttempted { get; init; }
    public int PushOperationsSucceeded { get; init; }
    public int PushOperationsFailed { get; init; }
    public int PushOperationsRetried { get; init; }
    public int PushOperationsPermanentlyFailed { get; init; }
    
    // Pull metrics
    public int PullCategoriesReceived { get; init; }
    public int PullLocationsReceived { get; init; }
    public int PullDepartmentsReceived { get; init; }
    public int PullAssetsReceived { get; init; }
    public int PullDeletedItemsReceived { get; init; }
    public int PullItemsSkipped { get; init; }
    
    // Conflict resolution
    public int ConflictsDetected { get; init; }
    public int ConflictsResolved { get; init; }
    
    // Bandwidth (approximate)
    public long BytesTransferred { get; init; }
    
    // Overall status
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    
    public int TotalItemsSynced =>
        PushOperationsSucceeded +
        PullCategoriesReceived +
        PullLocationsReceived +
        PullDepartmentsReceived +
        PullAssetsReceived;
}

/// <summary>
/// ENHANCEMENT #7: Delta/patch format for bandwidth optimization
/// Only sends changed fields instead of full entity
/// </summary>
public record AssetPatchDTO
{
    public string AssetId { get; init; } = string.Empty;
    
    /// <summary>
    /// Dictionary of changed fields: Key = property name, Value = new value
    /// Example: { "Name": "New Name", "Status": "Active", "LocationId": "loc123" }
    /// </summary>
    public Dictionary<string, object?> Changes { get; init; } = new();
    
    /// <summary>
    /// Timestamp when changes were made (for conflict resolution)
    /// </summary>
    public DateTime? DateModified { get; init; }
    
    /// <summary>
    /// Calculate approximate size in bytes for bandwidth tracking
    /// </summary>
    public long EstimatedSizeBytes
    {
        get
        {
            long size = AssetId.Length * 2; // UTF-16 encoding
            foreach (var kvp in Changes)
            {
                size += kvp.Key.Length * 2;
                size += kvp.Value?.ToString()?.Length * 2 ?? 0;
            }
            return size;
        }
    }
}

/// <summary>
/// ENHANCEMENT #7: Patch operation for sync queue
/// </summary>
public record SyncPatchOperationDTO
{
    public int QueueItemId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty; // "PATCH"
    public AssetPatchDTO PatchData { get; init; } = new();
    public DateTime CreatedAt { get; init; }
}