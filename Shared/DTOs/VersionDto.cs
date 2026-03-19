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
    string[] Features
);

/// <summary>
/// DTO for version check request
/// </summary>
public record VersionCheckRequestDto(
    string Platform,
    string CurrentVersion
);