namespace MobileApp.Services;

/// <summary>
/// Service for synchronizing data between mobile app and server
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Push pending changes from mobile to server
    /// </summary>
    Task<(bool Success, string Message)> PushChangesAsync();

    /// <summary>
    /// Pull changes from server to mobile
    /// </summary>
    Task<(bool Success, string Message)> PullChangesAsync();

    /// <summary>
    /// Perform full sync (push then pull)
    /// </summary>
    Task<(bool Success, string Message)> FullSyncAsync();

    /// <summary>
    /// Get count of pending sync operations
    /// </summary>
    Task<int> GetPendingSyncCountAsync();

    /// <summary>
    /// Enqueue a push sync request to be processed by the background sync queue.
    /// Returns when the request has been queued; caller may choose not to await the task.
    /// </summary>
    Task<(bool Success, string Message)> EnqueuePushAsync();

    /// <summary>
    /// Enqueue a full sync (push then pull) to be processed by the background sync queue.
    /// </summary>
    Task<(bool Success, string Message)> EnqueueFullSyncAsync();

    /// <summary>
    /// Reset sync state to force full re-sync from server
    /// </summary>
    
    /// <summary>
    /// Clear all local data (assets, categories, locations, departments, sync queue)
    /// </summary>
    Task ClearAllLocalDataAsync();
    Task ResetSyncStateAsync();
}