using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;
using Shared.Helpers;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            Channels = new
            {
                Stable = "Production users: latest non-prerelease mobile-v* release only",
                Beta = "Testers: latest pre-release or stable by SemVer (RC + stable)"
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
            var channel = SemanticVersion.NormalizeChannel(request.Channel);

            _logger.LogInformation(
                "Version check requested - Platform: {Platform}, Current: {Version}, Channel: {Channel}",
                request.Platform, request.CurrentVersion, channel);

            var versionInfo = await GetLatestVersionInfoAsync(request.Platform, channel);

            if (versionInfo is null)
            {
                return BadRequest("Unable to retrieve version information");
            }

            _logger.LogInformation(
                "Returning version info - Latest: {Latest}, Minimum: {Minimum}, Channel: {Channel}, IsPrerelease: {IsPrerelease}",
                versionInfo.LatestVersion, versionInfo.MinimumSupportedVersion,
                versionInfo.Channel, versionInfo.IsPrerelease);

            return Ok(versionInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking version for platform {Platform}", request.Platform);
            return StatusCode(500, "Error checking for updates");
        }
    }

    /// <summary>
    /// Get the latest version information from GitHub releases for the given channel.
    /// stable → newest non-prerelease mobile release.
    /// beta → newest among pre-releases and stables (SemVer: RC + stable).
    /// </summary>
    private async Task<VersionCheckResponseDto?> GetLatestVersionInfoAsync(string platform, string channel)
    {
        try
        {
            if (!platform.Equals("android", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var githubOwner = _configuration["GitHub:Owner"];
            var githubRepo = _configuration["GitHub:Repository"];

            if (string.IsNullOrEmpty(githubOwner) || string.IsNullOrEmpty(githubRepo))
            {
                _logger.LogWarning("GitHub configuration missing. Owner: {Owner}, Repo: {Repo}",
                    githubOwner ?? "NULL", githubRepo ?? "NULL");
                return GetFallbackVersionInfo(channel);
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "AssetTag-Mobile-App");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

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
                return GetFallbackVersionInfo(channel);
            }

            var content = await response.Content.ReadAsStringAsync();
            var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (releases is null || releases.Count == 0)
            {
                _logger.LogWarning("No releases found in GitHub");
                return GetFallbackVersionInfo(channel);
            }

            var mobileReleases = releases
                .Where(r => r.TagName.StartsWith("mobile-v", StringComparison.OrdinalIgnoreCase))
                .Where(r => r.Assets.Any(a => a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (mobileReleases.Count == 0)
            {
                _logger.LogWarning("No mobile releases with APK found. Total releases: {Count}", releases.Count);
                _logger.LogInformation("Available tags: {Tags}",
                    string.Join(", ", releases.Select(r => r.TagName)));
                return GetFallbackVersionInfo(channel);
            }

            // Production: non-prerelease only.
            // Beta: pre-releases and stables; SemVer picks winner (1.0.2-rc.1 < 1.0.2).
            IEnumerable<GitHubRelease> candidates = channel == SemanticVersion.BetaChannel
                ? mobileReleases
                : mobileReleases.Where(r => !r.Prerelease);

            var latestRelease = candidates
                .OrderByDescending(r => SemanticVersion.FromMobileTag(r.TagName), Comparer<string>.Create(SemanticVersion.Compare))
                .ThenByDescending(r => r.PublishedAt)
                .FirstOrDefault();

            if (latestRelease is null)
            {
                _logger.LogWarning(
                    "No mobile releases for channel {Channel}. Mobile with APK: {Count}",
                    channel, mobileReleases.Count);
                return GetFallbackVersionInfo(channel);
            }

            var version = SemanticVersion.FromMobileTag(latestRelease.TagName);
            var apkAsset = latestRelease.Assets
                .First(a => a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase));

            var minimumVersion = _configuration["MobileApp:MinimumSupportedVersion"] ?? "1.0.0";
            var features = ParseFeaturesFromReleaseNotes(latestRelease.Body);

            _logger.LogInformation(
                "Selected release {Tag} (prerelease={IsPrerelease}) for channel {Channel}",
                latestRelease.TagName, latestRelease.Prerelease, channel);

            // IsMandatory for a specific device is computed on the client from MinimumSupportedVersion.
            return new VersionCheckResponseDto(
                LatestVersion: version,
                MinimumSupportedVersion: minimumVersion,
                DownloadUrl: apkAsset.BrowserDownloadUrl,
                ReleaseNotesUrl: latestRelease.HtmlUrl,
                FileSize: apkAsset.Size,
                Checksum: string.Empty,
                IsMandatory: false,
                ReleaseDate: latestRelease.PublishedAt,
                Features: features,
                Channel: channel,
                IsPrerelease: latestRelease.Prerelease
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching version info from GitHub");
            return GetFallbackVersionInfo(channel);
        }
    }

    private VersionCheckResponseDto GetFallbackVersionInfo(string channel)
    {
        // Fallback config always describes production/stable.
        // Beta clients still receive it when GitHub has nothing usable.
        var latestVersion = _configuration["MobileApp:LatestVersion"] ?? "1.0.0";
        var minimumVersion = _configuration["MobileApp:MinimumSupportedVersion"] ?? "1.0.0";
        var downloadUrl = _configuration["MobileApp:DownloadUrl"] ?? "";

        _logger.LogInformation(
            "Using fallback version info: {Version} (requested channel: {Channel})",
            latestVersion, channel);

        return new VersionCheckResponseDto(
            LatestVersion: latestVersion,
            MinimumSupportedVersion: minimumVersion,
            DownloadUrl: downloadUrl,
            ReleaseNotesUrl: "",
            FileSize: 0,
            Checksum: "",
            IsMandatory: false,
            ReleaseDate: DateTime.UtcNow,
            Features: Array.Empty<string>(),
            Channel: channel,
            IsPrerelease: false
        );
    }

    private static string[] ParseFeaturesFromReleaseNotes(string? releaseBody)
    {
        if (string.IsNullOrWhiteSpace(releaseBody))
        {
            return Array.Empty<string>();
        }

        var lines = releaseBody.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines
            .Where(line => line.TrimStart().StartsWith("-") || line.TrimStart().StartsWith("*"))
            .Select(line => line.TrimStart('-', '*', ' ').Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(5)
            .ToArray();
    }

    #region GitHub API Models

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = new();
    }

    private class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }

    #endregion
}
