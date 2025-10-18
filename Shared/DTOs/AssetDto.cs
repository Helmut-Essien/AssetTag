using System;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs;

public record AssetCreateDTO
{
    [Required]
    public string AssetTag { get; init; } = string.Empty;

    [Required]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    [Required]
    public string CategoryId { get; init; } = string.Empty;

    [Required]
    public string LocationId { get; init; } = string.Empty;

    [Required]
    public string DepartmentId { get; init; } = string.Empty;

    public DateTime? PurchaseDate { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? CurrentValue { get; init; }

    [Required]
    public string Status { get; init; } = string.Empty;

    public string? AssignedToUserId { get; init; }
    public string? SerialNumber { get; init; }

    [Required]
    public string Condition { get; init; } = string.Empty;
}

public record AssetUpdateDTO
{
    [Required]
    public string AssetId { get; init; } = string.Empty;
    public string? AssetTag { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? CategoryId { get; init; }
    public string? LocationId { get; init; }
    public string? DepartmentId { get; init; }
    public DateTime? PurchaseDate { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? CurrentValue { get; init; }
    public string? Status { get; init; }
    public string? AssignedToUserId { get; init; }
    public string? SerialNumber { get; init; }
    public string? Condition { get; init; }
}

public record AssetReadDTO
{
    public string AssetId { get; init; } = string.Empty;
    public string AssetTag { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string CategoryId { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public string DepartmentId { get; init; } = string.Empty;
    public DateTime? PurchaseDate { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? CurrentValue { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? AssignedToUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime DateModified { get; init; }
    public string? SerialNumber { get; init; }
    public string Condition { get; init; } = string.Empty;
}