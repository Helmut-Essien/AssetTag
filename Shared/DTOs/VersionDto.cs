namespace Shared.DTOs;

/// <summary>
/// DTO for version check response from the server
/// </summary>
public record VersionCheckResponseDto(
    string LatestVersion,
    string MinimumSupportedVersion,
    string DownloadUrl,
    string ReleaseNotesUrl,
    long FileSize,
    string Checksum,
    bool IsMandatory,
    DateTime ReleaseDate,
    string[] Features,
    /// <summary>Channel used to resolve this version: stable or beta.</summary>
    string Channel = "stable",
    /// <summary>True when the selected GitHub release is a pre-release.</summary>
    bool IsPrerelease = false
);

/// <summary>
/// DTO for version check request
/// </summary>
/// <param name="Platform">Client platform (e.g. android).</param>
/// <param name="CurrentVersion">Installed app version string.</param>
/// <param name="Channel">
/// Update channel: "stable" (production, default) or "beta"
/// (pre-releases plus stable; newest SemVer wins).
/// </param>
public record VersionCheckRequestDto(
    string Platform,
    string CurrentVersion,
    string? Channel = null
);
