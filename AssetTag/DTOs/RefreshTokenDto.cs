using System;

namespace AssetTag.DTOs;

public record RefreshTokenReadDTO
{
    public int Id { get; init; }
    public string Token { get; init; } = string.Empty;
    public DateTime Expires { get; init; }
    public DateTime Created { get; init; }
    public string CreatedByIp { get; init; } = string.Empty;
    public DateTime? Revoked { get; init; }
    public string? RevokedByIp { get; init; }
    public string? ReplacedByToken { get; init; }
    public bool IsExpired { get; init; }
    public bool IsActive { get; init; }
}