using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs;

public record DepartmentCreateDTO
{
    [Required]
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public record DepartmentUpdateDTO
{
    [Required]
    public string DepartmentId { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Description { get; init; }
}

public record DepartmentReadDTO
{
    public string DepartmentId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}