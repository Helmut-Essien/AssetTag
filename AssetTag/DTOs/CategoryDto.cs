using System.ComponentModel.DataAnnotations;

namespace AssetTag.DTOs;

public record CategoryCreateDTO
{
    [Required]
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public record CategoryUpdateDTO
{
    [Required]
    public string CategoryId { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Description { get; init; }
}

public record CategoryReadDTO
{
    public string CategoryId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}