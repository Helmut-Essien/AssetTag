namespace MobileApp.Services;

public enum SyncPhase
{
    Starting,
    PushingChanges,
    PullingCategories,
    PullingLocations,
    PullingDepartments,
    PullingAssets,
    Finalizing,
    Completed,
    Failed
}

public class SyncProgressEventArgs : EventArgs
{
    public SyncPhase Phase { get; set; }
    public int CurrentItem { get; set; }
    public int TotalItems { get; set; }
    public string Message { get; set; } = string.Empty;

    public double NormalizedProgress => Phase switch
    {
        SyncPhase.Starting => 0.05,
        SyncPhase.PushingChanges => TotalItems > 0
            ? 0.10 + (0.30 * CurrentItem / TotalItems)
            : 0.20,
        SyncPhase.PullingCategories => 0.45,
        SyncPhase.PullingLocations => 0.55,
        SyncPhase.PullingDepartments => 0.65,
        SyncPhase.PullingAssets => TotalItems > 0
            ? 0.70 + (0.20 * CurrentItem / TotalItems)
            : 0.75,
        SyncPhase.Finalizing => 0.95,
        SyncPhase.Completed => 1.0,
        SyncPhase.Failed => 0,
        _ => 0
    };
}

/// <summary>
/// Service for synchronizing data between mobile app and server.
/// Registered as Singleton so tab ViewModels and BackgroundSync share one queue.
/// Each DB/HTTP call creates a short-lived DI scope internally.
/// </summary>
public interface ISyncService
{
    event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;

    Task<(bool Success, string Message)> PushChangesAsync();
    Task<(bool Success, string Message)> PullChangesAsync();
    Task<(bool Success, string Message)> FullSyncAsync();
    Task<int> GetPendingSyncCountAsync();
    Task<(bool Success, string Message)> EnqueuePushAsync();
    Task<(bool Success, string Message)> EnqueueFullSyncAsync();
    Task ClearAllLocalDataAsync();
    Task ResetSyncStateAsync();
}
