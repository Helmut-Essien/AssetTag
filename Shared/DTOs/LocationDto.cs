using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs;

public record LocationCreateDTO
{
    [Required]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    [Required]
    public string Campus { get; init; } = string.Empty;

    public string? Building { get; init; }
    public string? Room { get; init; }

    [Range(-90.0, 90.0)]
    public double? Latitude { get; init; }

    [Range(-180.0, 180.0)]
    public double? Longitude { get; init; }
}

public record LocationUpdateDTO
{
    //[Required]
    //public string LocationId { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Campus { get; init; }
    public string? Building { get; init; }
    public string? Room { get; init; }
    [Range(-90.0, 90.0)]
    public double? Latitude { get; init; }
    [Range(-180.0, 180.0)]
    public double? Longitude { get; init; }
}

public record LocationReadDTO(string LocationId, string Name, string? Description, string Campus, string? Building, string? Room, double? Latitude, double? Longitude);
