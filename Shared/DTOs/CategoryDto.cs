using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs;

public record CategoryCreateDTO
{
    [Required]
    [Display(Name = "Category Name")]
    public string Name { get; init; } = string.Empty;
    
    [Display(Name = "Description")]
    public string? Description { get; init; }
    
    [Display(Name = "Depreciation Rate (%)")]
    [Range(0, 100, ErrorMessage = "Depreciation rate must be between 0 and 100%")]
    public decimal? DepreciationRate { get; init; }
}

public record CategoryUpdateDTO
{
    [Required]
    public string CategoryId { get; init; } = string.Empty;
    
    [Display(Name = "Category Name")]
    public string? Name { get; init; }
    
    [Display(Name = "Description")]
    public string? Description { get; init; }
    
    [Display(Name = "Depreciation Rate (%)")]
    [Range(0, 100, ErrorMessage = "Depreciation rate must be between 0 and 100%")]
    public decimal? DepreciationRate { get; init; }
}

// Positional read DTO (immutable, good for EF projection)
public record CategoryReadDTO(
    string CategoryId,
    string Name,
    string? Description,
    decimal? DepreciationRate
);