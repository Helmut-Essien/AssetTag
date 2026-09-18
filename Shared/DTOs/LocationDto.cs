using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs;

public record LocationCreateDTO
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; init; }

    [Required]
    [StringLength(200)]
    public string Campus { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Building { get; init; }

    [StringLength(100)]
    public string? Room { get; init; }

    [Range(-90.0, 90.0)]
    public double? Latitude { get; init; }

    [Range(-180.0, 180.0)]
    public double? Longitude { get; init; }
}

public record LocationUpdateDTO
{
    [Required]
    [StringLength(26)] // ULID
    public string LocationId { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Name { get; init; }

    [StringLength(1000)]
    public string? Description { get; init; }

    [StringLength(200)]
    public string? Campus { get; init; }

    [StringLength(200)]
    public string? Building { get; init; }

    [StringLength(100)]
    public string? Room { get; init; }

    [Range(-90.0, 90.0)]
    public double? Latitude { get; init; }

    [Range(-180.0, 180.0)]
    public double? Longitude { get; init; }
}

public record LocationReadDTO(string LocationId, string Name, string? Description, string Campus, string? Building, string? Room, double? Latitude, double? Longitude);
