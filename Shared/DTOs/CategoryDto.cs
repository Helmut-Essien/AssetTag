using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs;

public record CategoryCreateDTO
{
    [Required]
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int? DepreciationRate { get; init; }
}

public record CategoryUpdateDTO
{
    [Required]
    public string CategoryId { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Description { get; init; }
    
    public int? DepreciationRate { get; init; }
}

// Positional read DTO (immutable, good for EF projection)
public record CategoryReadDTO(string CategoryId, string Name, string? Description, int? DepreciationRate);