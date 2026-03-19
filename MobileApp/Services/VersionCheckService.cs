using Shared.DTOs;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace MobileApp.Services;

/// <summary>
/// Service for checking and managing app version updates
/// Implements efficient .NET 9 patterns with proper resource management
/// </summary>
public sealed class VersionCheckService : IVersionCheckService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VersionCheckService> _logger;
    private const string LAST_CHECK_KEY = "last_version_check";
    private const string UPDATE_DISMISSED_KEY = "update_dismissed_version";

    public VersionCheckService(
        IHttpClientFactory httpClientFactory,
        ILogger<VersionCheckService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string GetCurrentVersion()
    {
        return AppInfo.VersionString;
    }

    public async Task<(bool UpdateAvailable, VersionCheckResponseDto? VersionInfo, string Message)> CheckForUpdateAsync()
    {
        try
        {
            // Check network connectivity
            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                return (false, null, "No internet connection");
            }

            var currentVersion = GetCurrentVersion();
            var platform = DeviceInfo.Platform.ToString().ToLowerInvariant();

            using var httpClient = _httpClientFactory.CreateClient("ApiClient");
            
            var request = new VersionCheckRequestDto(platform, currentVersion);
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await httpClient.PostAsJsonAsync("api/mobile/version/check", request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Version check failed with status: {StatusCode}", response.StatusCode);
                return (false, null, $"Version check failed: {response.StatusCode}");
            }

            var versionInfo = await response.Content.ReadFromJsonAsync<VersionCheckResponseDto>(cancellationToken: cts.Token);

            if (versionInfo is null)
            {
                return (false, null, "Invalid response from server");
            }

            // Update last check time
            await SetLastCheckTimeAsync(DateTime.UtcNow);

            // Compare versions using semantic versioning
            var updateAvailable = CompareVersions(currentVersion, versionInfo.LatestVersion) < 0;

            if (!updateAvailable)
            {
                _logger.LogInformation("App is up to date. Current: {Current}, Latest: {Latest}", 
                    currentVersion, versionInfo.LatestVersion);
                return (false, versionInfo, "App is up to date");
            }

            // Check if this is a mandatory update
            var isMandatory = CompareVersions(currentVersion, versionInfo.MinimumSupportedVersion) < 0;

            var message = isMandatory 
                ? $"Critical update required to version {versionInfo.LatestVersion}" 
                : $"Version {versionInfo.LatestVersion} is available";

            _logger.LogInformation("Update available: {Latest} (Current: {Current}, Mandatory: {Mandatory})", 
                versionInfo.LatestVersion, currentVersion, isMandatory);

            return (true, versionInfo, message);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Version check timed out");
            return (false, null, "Request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during version check");
            return (false, null, $"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during version check");
            return (false, null, $"Error checking for updates: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DownloadAndInstallUpdateAsync(
        VersionCheckResponseDto versionInfo, 
        IProgress<double>? progress = null)
    {
#if ANDROID
        try
        {
            _logger.LogInformation("Starting download of version {Version}", versionInfo.LatestVersion);

            // Create downloads directory
            var downloadsPath = Path.Combine(FileSystem.CacheDirectory, "updates");
            Directory.CreateDirectory(downloadsPath);

            var fileName = $"MUGAssets-v{versionInfo.LatestVersion}.apk";
            var filePath = Path.Combine(downloadsPath, fileName);

            // Delete existing file if present
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // Download the APK
            using var httpClient = _httpClientFactory.CreateClient("ApiClient");
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            
            using var response = await httpClient.GetAsync(versionInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            
            if (!response.IsSuccessStatusCode)
            {
                return (false, $"Download failed: {response.StatusCode}");
            }

            var totalBytes = response.Content.Headers.ContentLength ?? versionInfo.FileSize;
            var downloadedBytes = 0L;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);
            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cts.Token)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);
                downloadedBytes += bytesRead;

                // Report progress
                if (totalBytes > 0)
                {
                    var progressPercentage = (double)downloadedBytes / totalBytes;
                    progress?.Report(progressPercentage);
                }
            }

            await fileStream.FlushAsync(cts.Token);

            _logger.LogInformation("Download completed: {FilePath}", filePath);

            // Verify checksum
            if (!string.IsNullOrEmpty(versionInfo.Checksum))
            {
                var fileChecksum = await ComputeFileChecksumAsync(filePath);
                if (!string.Equals(fileChecksum, versionInfo.Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(filePath);
                    _logger.LogError("Checksum mismatch. Expected: {Expected}, Got: {Actual}", 
                        versionInfo.Checksum, fileChecksum);
                    return (false, "Download verification failed. Please try again.");
                }
            }

            // Install the APK
            await InstallApkAsync(filePath);

            return (true, "Update downloaded successfully. Please complete installation.");
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Download timed out");
            return (false, "Download timed out. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading update");
            return (false, $"Download failed: {ex.Message}");
        }
#else
        await Task.CompletedTask;
        return (false, "Auto-update is only supported on Android");
#endif
    }

#if ANDROID
    private static async Task<string> ComputeFileChecksumAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static async Task InstallApkAsync(string apkPath)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                var file = new Java.IO.File(apkPath);
                var context = Android.App.Application.Context;

                Android.Net.Uri? apkUri;

                // Use FileProvider for Android 7.0+ (API 24+)
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.N)
                {
                    apkUri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                        context,
                        $"{context.PackageName}.fileprovider",
                        file);
                }
                else
                {
                    apkUri = Android.Net.Uri.FromFile(file);
                }

                var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
                intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
                intent.SetFlags(Android.Content.ActivityFlags.NewTask | Android.Content.ActivityFlags.GrantReadUriPermission);

                context.StartActivity(intent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error installing APK: {ex.Message}");
                throw;
            }
        });
    }
#endif

    public async Task<DateTime?> GetLastCheckTimeAsync()
    {
        try
        {
            var lastCheckStr = await SecureStorage.GetAsync(LAST_CHECK_KEY);
            if (string.IsNullOrEmpty(lastCheckStr))
            {
                return null;
            }

            if (DateTime.TryParse(lastCheckStr, out var lastCheck))
            {
                return lastCheck;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetLastCheckTimeAsync(DateTime checkTime)
    {
        try
        {
            await SecureStorage.SetAsync(LAST_CHECK_KEY, checkTime.ToString("O"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save last check time");
        }
    }

    /// <summary>
    /// Compare two semantic version strings.
    /// Returns -1 if v1 is less than v2, 0 if equal, 1 if v1 is greater than v2.
    /// </summary>
    private static int CompareVersions(string version1, string version2)
    {
        var v1Parts = version1.Split('.').Select(int.Parse).ToArray();
        var v2Parts = version2.Split('.').Select(int.Parse).ToArray();

        var maxLength = Math.Max(v1Parts.Length, v2Parts.Length);

        for (int i = 0; i < maxLength; i++)
        {
            var v1Part = i < v1Parts.Length ? v1Parts[i] : 0;
            var v2Part = i < v2Parts.Length ? v2Parts[i] : 0;

            if (v1Part < v2Part) return -1;
            if (v1Part > v2Part) return 1;
        }

        return 0;
    }
}