using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;
using System.Text.Json;

namespace AssetTag.Controllers;

/// <summary>
/// Controller for mobile app version management and update checks
/// </summary>
[ApiController]
[Route("api/mobile/version")]
public class MobileVersionController : ControllerBase
{
    private readonly ILogger<MobileVersionController> _logger;
    private readonly IConfiguration _configuration;

    public MobileVersionController(
        ILogger<MobileVersionController> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Test endpoint to check version configuration (GET request for browser testing)
    /// </summary>
    [HttpGet("test")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult TestVersionConfig()
    {
        var githubOwner = _configuration["GitHub:Owner"];
        var githubRepo = _configuration["GitHub:Repository"];
        var githubToken = _configuration["GitHub:Token"];
        var latestVersion = _configuration["MobileApp:LatestVersion"];
        var minimumVersion = _configuration["MobileApp:MinimumSupportedVersion"];
        var downloadUrl = _configuration["MobileApp:DownloadUrl"];

        return Ok(new
        {
            GitHubConfig = new
            {
                Owner = githubOwner ?? "NOT SET",
                Repository = githubRepo ?? "NOT SET",
                HasToken = !string.IsNullOrEmpty(githubToken)
            },
            FallbackConfig = new
            {
                LatestVersion = latestVersion ?? "NOT SET",
                MinimumVersion = minimumVersion ?? "NOT SET",
                DownloadUrl = downloadUrl ?? "NOT SET"
            },
            Message = "This shows what the API can see in configuration"
        });
    }

    /// <summary>
    /// Check for available app updates
    /// </summary>
    [HttpPost("check")]
    [ProducesResponseType(typeof(VersionCheckResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VersionCheckResponseDto>> CheckVersion(
        [FromBody] VersionCheckRequestDto request)
    {
        try
        {
            _logger.LogInformation("Version check requested - Platform: {Platform}, Current: {Version}",
                request.Platform, request.CurrentVersion);

            // Get version info from configuration or GitHub API
            var versionInfo = await GetLatestVersionInfoAsync(request.Platform);

            if (versionInfo is null)
            {
                return BadRequest("Unable to retrieve version information");
            }

            _logger.LogInformation("Returning version info - Latest: {Latest}, Minimum: {Minimum}",
                versionInfo.LatestVersion, versionInfo.MinimumSupportedVersion);

            return Ok(versionInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking version for platform {Platform}", request.Platform);
            return StatusCode(500, "Error checking for updates");
        }
    }

    /// <summary>
    /// Get the latest version information from GitHub releases
    /// </summary>
    private async Task<VersionCheckResponseDto?> GetLatestVersionInfoAsync(string platform)
    {
        try
        {
            // For Android platform
            if (platform.Equals("android", StringComparison.OrdinalIgnoreCase))
            {
                // Get GitHub repository info from configuration
                var githubOwner = _configuration["GitHub:Owner"];
                var githubRepo = _configuration["GitHub:Repository"];

                // If GitHub config is missing, use fallback immediately
                if (string.IsNullOrEmpty(githubOwner) || string.IsNullOrEmpty(githubRepo))
                {
                    _logger.LogWarning("GitHub configuration missing. Owner: {Owner}, Repo: {Repo}", 
                        githubOwner ?? "NULL", githubRepo ?? "NULL");
                    return GetFallbackVersionInfo();
                }

                // Fetch latest release from GitHub API
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "AssetTag-Mobile-App");
                httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

                // Add GitHub token if available for higher rate limits
                var githubToken = _configuration["GitHub:Token"];
                if (!string.IsNullOrEmpty(githubToken))
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {githubToken}");
                }

                var apiUrl = $"https://api.github.com/repos/{githubOwner}/{githubRepo}/releases";
                _logger.LogInformation("Fetching releases from: {Url}", apiUrl);
                
                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GitHub API request failed: {StatusCode}", response.StatusCode);
                    return GetFallbackVersionInfo();
                }

                var content = await response.Content.ReadAsStringAsync();
                var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (releases is null || releases.Count == 0)
                {
                    _logger.LogWarning("No releases found in GitHub");
                    return GetFallbackVersionInfo();
                }

                // Find the latest mobile release (including pre-releases for testing)
                var latestRelease = releases
                    .Where(r => r.TagName.StartsWith("mobile-v", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(r => r.PublishedAt)
                    .FirstOrDefault();

                if (latestRelease is null)
                {
                    _logger.LogWarning("No mobile releases found. Total releases: {Count}", releases.Count);
                    _logger.LogInformation("Available tags: {Tags}",
                        string.Join(", ", releases.Select(r => r.TagName)));
                    return GetFallbackVersionInfo();
                }

                _logger.LogInformation("Found release: {Tag}, IsPrerelease: {IsPrerelease}",
                    latestRelease.TagName, latestRelease.Prerelease);

                _logger.LogInformation("Found latest release: {Tag}", latestRelease.TagName);

                // Extract version from tag (e.g., "mobile-v1.0.123" -> "1.0.123")
                var version = latestRelease.TagName.Replace("mobile-v", "", StringComparison.OrdinalIgnoreCase);

                // Find the APK asset
                var apkAsset = latestRelease.Assets
                    .FirstOrDefault(a => a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase));

                if (apkAsset is null)
                {
                    _logger.LogWarning("No APK found in release {Tag}", latestRelease.TagName);
                    return GetFallbackVersionInfo();
                }

                // Get minimum supported version from configuration
                var minimumVersion = _configuration["MobileApp:MinimumSupportedVersion"] ?? "1.0.0";

                // Parse features from release body
                var features = ParseFeaturesFromReleaseNotes(latestRelease.Body);

                _logger.LogInformation("Successfully retrieved version info from GitHub: {Version}", version);

                return new VersionCheckResponseDto(
                    LatestVersion: version,
                    MinimumSupportedVersion: minimumVersion,
                    DownloadUrl: apkAsset.BrowserDownloadUrl,
                    ReleaseNotesUrl: latestRelease.HtmlUrl,
                    FileSize: apkAsset.Size,
                    Checksum: string.Empty, // GitHub doesn't provide checksums directly
                    IsMandatory: IsVersionMandatory(version, minimumVersion),
                    ReleaseDate: latestRelease.PublishedAt,
                    Features: features
                );
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching version info from GitHub");
            return GetFallbackVersionInfo();
        }
    }

    /// <summary>
    /// Get fallback version info from configuration
    /// </summary>
    private VersionCheckResponseDto GetFallbackVersionInfo()
    {
        var latestVersion = _configuration["MobileApp:LatestVersion"] ?? "1.0.0";
        var minimumVersion = _configuration["MobileApp:MinimumSupportedVersion"] ?? "1.0.0";
        var downloadUrl = _configuration["MobileApp:DownloadUrl"] ?? "";

        _logger.LogInformation("Using fallback version info: {Version}", latestVersion);

        return new VersionCheckResponseDto(
            LatestVersion: latestVersion,
            MinimumSupportedVersion: minimumVersion,
            DownloadUrl: downloadUrl,
            ReleaseNotesUrl: "",
            FileSize: 0,
            Checksum: "",
            IsMandatory: false,
            ReleaseDate: DateTime.UtcNow,
            Features: Array.Empty<string>()
        );
    }

    /// <summary>
    /// Determine if an update is mandatory based on version comparison
    /// </summary>
    private static bool IsVersionMandatory(string currentVersion, string minimumVersion)
    {
        try
        {
            var current = Version.Parse(currentVersion);
            var minimum = Version.Parse(minimumVersion);
            return current < minimum;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parse features from GitHub release notes
    /// </summary>
    private static string[] ParseFeaturesFromReleaseNotes(string? releaseBody)
    {
        if (string.IsNullOrWhiteSpace(releaseBody))
        {
            return Array.Empty<string>();
        }

        // Extract bullet points or numbered items
        var lines = releaseBody.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var features = lines
            .Where(line => line.TrimStart().StartsWith("-") || line.TrimStart().StartsWith("*"))
            .Select(line => line.TrimStart('-', '*', ' ').Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(5) // Limit to top 5 features
            .ToArray();

        return features;
    }

    #region GitHub API Models

    private class GitHubRelease
    {
        public string TagName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool Prerelease { get; set; }
        public DateTime PublishedAt { get; set; }
        public string HtmlUrl { get; set; } = string.Empty;
        public List<GitHubAsset> Assets { get; set; } = new();
    }

    private class GitHubAsset
    {
        public string Name { get; set; } = string.Empty;
        public string BrowserDownloadUrl { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    #endregion
}