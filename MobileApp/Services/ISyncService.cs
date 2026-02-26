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
    /// Reset sync state to force full re-sync from server
    /// </summary>
    Task ResetSyncStateAsync();
}