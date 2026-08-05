using Shared.DTOs;

namespace MobileApp.Services;

/// <summary>
/// Service interface for checking app version updates
/// </summary>
public interface IVersionCheckService
{
    /// <summary>
    /// Check if a new version is available for the user's selected channel
    /// </summary>
    Task<(bool UpdateAvailable, VersionCheckResponseDto? VersionInfo, string Message)> CheckForUpdateAsync();

    /// <summary>
    /// Download and install the update (Android only)
    /// </summary>
    Task<(bool Success, string Message)> DownloadAndInstallUpdateAsync(VersionCheckResponseDto versionInfo, IProgress<double>? progress = null);

    /// <summary>
    /// Get the current app version
    /// </summary>
    string GetCurrentVersion();

    /// <summary>
    /// Get the last time an update check was performed
    /// </summary>
    Task<DateTime?> GetLastCheckTimeAsync();

    /// <summary>
    /// Set the last time an update check was performed
    /// </summary>
    Task SetLastCheckTimeAsync(DateTime checkTime);

    /// <summary>
    /// Whether the user opted into beta updates (RC + stable).
    /// Production default is false (stable only).
    /// </summary>
    bool IsBetaUpdatesEnabled { get; }

    /// <summary>
    /// Enable or disable the beta update channel.
    /// </summary>
    void SetBetaUpdatesEnabled(bool enabled);

    /// <summary>
    /// Active update channel: "stable" or "beta".
    /// </summary>
    string GetUpdateChannel();
}
