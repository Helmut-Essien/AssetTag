using System;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs;

public record AssetCreateDTO
{
    [Required]
    [Display(Name = "Asset Tag")]
    public string AssetTag { get; init; } = string.Empty;

    [Required]
    [Display(Name = "Asset Name")]
    public string Name { get; init; } = string.Empty;

    [Display(Name = "Description")]
    public string? Description { get; init; }

    [Required]
    [Display(Name = "Category")]
    public string CategoryId { get; init; } = string.Empty;

    [Required]
    [Display(Name = "Location")]
    public string LocationId { get; init; } = string.Empty;

    [Required]
    [Display(Name = "Department")]
    public string DepartmentId { get; init; } = string.Empty;

    [Display(Name = "Purchase Date")]
    public DateTime? PurchaseDate { get; init; }

    [Display(Name = "Purchase Price")]
    public decimal? PurchasePrice { get; init; }

    [Display(Name = "Current Value")]
    public decimal? CurrentValue { get; init; }

    [Required]
    [Display(Name = "Status")]
    public string Status { get; init; } = string.Empty;

    [Display(Name = "Assigned To User")]
    public string? AssignedToUserId { get; init; }

    [Display(Name = "Serial Number")]
    public string? SerialNumber { get; init; }

    [Required]
    [Display(Name = "Condition")]
    public string Condition { get; init; } = string.Empty;

    // New fields from Excel
    [Display(Name = "Vendor Name")]
    public string? VendorName { get; init; }

    [Display(Name = "Invoice Number")]
    public string? InvoiceNumber { get; init; }

    [Display(Name = "Quantity")]
    public int Quantity { get; init; } = 1;

    [Display(Name = "Cost Per Unit")]
    public decimal? CostPerUnit { get; init; }

    [Display(Name = "Total Cost")]
    public decimal? TotalCost { get; init; }

    [Display(Name = "Depreciation Rate (%)")]
    public decimal? DepreciationRate { get; init; }

    [Display(Name = "Accumulated Depreciation")]
    public decimal? AccumulatedDepreciation { get; init; }

    [Display(Name = "Net Book Value")]
    public decimal? NetBookValue { get; init; }

    [Display(Name = "Useful Life (Years)")]
    public int? UsefulLifeYears { get; init; }

    [Display(Name = "Warranty Expiry")]
    public DateTime? WarrantyExpiry { get; init; }

    [Display(Name = "Disposal Date")]
    public DateTime? DisposalDate { get; init; }

    [Display(Name = "Disposal Value")]
    public decimal? DisposalValue { get; init; }

    [Display(Name = "Remarks")]
    public string? Remarks { get; init; }
}

public record AssetUpdateDTO
{
    [Required]
    [Display(Name = "Asset ID")]
    public string AssetId { get; init; } = string.Empty;

    [Display(Name = "Asset Tag")]
    public string? AssetTag { get; init; }

    [Display(Name = "Asset Name")]
    public string? Name { get; init; }

    [Display(Name = "Description")]
    public string? Description { get; init; }

    [Display(Name = "Category")]
    public string? CategoryId { get; init; }

    [Display(Name = "Location")]
    public string? LocationId { get; init; }

    [Display(Name = "Department")]
    public string? DepartmentId { get; init; }

    [Display(Name = "Purchase Date")]
    public DateTime? PurchaseDate { get; init; }

    [Display(Name = "Purchase Price")]
    public decimal? PurchasePrice { get; init; }

    [Display(Name = "Current Value")]
    public decimal? CurrentValue { get; init; }

    [Display(Name = "Status")]
    public string? Status { get; init; }

    [Display(Name = "Assigned To User")]
    public string? AssignedToUserId { get; init; }

    [Display(Name = "Serial Number")]
    public string? SerialNumber { get; init; }

    [Display(Name = "Condition")]
    public string? Condition { get; init; }

    // New fields from Excel
    [Display(Name = "Vendor Name")]
    public string? VendorName { get; init; }

    [Display(Name = "Invoice Number")]
    public string? InvoiceNumber { get; init; }

    [Display(Name = "Quantity")]
    public int? Quantity { get; init; }

    [Display(Name = "Cost Per Unit")]
    public decimal? CostPerUnit { get; init; }

    [Display(Name = "Total Cost")]
    public decimal? TotalCost { get; init; }

    [Display(Name = "Depreciation Rate (%)")]
    public decimal? DepreciationRate { get; init; }

    [Display(Name = "Accumulated Depreciation")]
    public decimal? AccumulatedDepreciation { get; init; }

    [Display(Name = "Net Book Value")]
    public decimal? NetBookValue { get; init; }

    [Display(Name = "Useful Life (Years)")]
    public int? UsefulLifeYears { get; init; }

    [Display(Name = "Warranty Expiry")]
    public DateTime? WarrantyExpiry { get; init; }

    [Display(Name = "Disposal Date")]
    public DateTime? DisposalDate { get; init; }

    [Display(Name = "Disposal Value")]
    public decimal? DisposalValue { get; init; }

    [Display(Name = "Remarks")]
    public string? Remarks { get; init; }
}

public record AssetReadDTO
{
    [Display(Name = "Asset ID")]
    public string AssetId { get; init; } = string.Empty;

    [Display(Name = "Asset Tag")]
    public string AssetTag { get; init; } = string.Empty;

    [Display(Name = "Asset Name")]
    public string Name { get; init; } = string.Empty;

    [Display(Name = "Description")]
    public string? Description { get; init; }

    [Display(Name = "Category")]
    public string CategoryId { get; init; } = string.Empty;

    [Display(Name = "Location")]
    public string LocationId { get; init; } = string.Empty;

    [Display(Name = "Department")]
    public string DepartmentId { get; init; } = string.Empty;

    [Display(Name = "Purchase Date")]
    public DateTime? PurchaseDate { get; init; }

    [Display(Name = "Purchase Price")]
    public decimal? PurchasePrice { get; init; }

    [Display(Name = "Current Value")]
    public decimal? CurrentValue { get; init; }

    [Display(Name = "Status")]
    public string Status { get; init; } = string.Empty;

    [Display(Name = "Assigned To User")]
    public string? AssignedToUserId { get; init; }

    [Display(Name = "Created At")]
    public DateTime CreatedAt { get; init; }

    [Display(Name = "Date Modified")]
    public DateTime DateModified { get; init; }

    [Display(Name = "Serial Number")]
    public string? SerialNumber { get; init; }

    [Display(Name = "Condition")]
    public string Condition { get; init; } = string.Empty;

    // New fields from Excel
    [Display(Name = "Vendor Name")]
    public string? VendorName { get; init; }

    [Display(Name = "Invoice Number")]
    public string? InvoiceNumber { get; init; }

    [Display(Name = "Quantity")]
    public int Quantity { get; init; } = 1;

    [Display(Name = "Cost Per Unit")]
    public decimal? CostPerUnit { get; init; }

    [Display(Name = "Total Cost")]
    public decimal? TotalCost { get; init; }

    [Display(Name = "Depreciation Rate (%)")]
    public decimal? DepreciationRate { get; init; }

    [Display(Name = "Accumulated Depreciation")]
    public decimal? AccumulatedDepreciation { get; init; }

    [Display(Name = "Net Book Value")]
    public decimal? NetBookValue { get; init; }

    [Display(Name = "Useful Life (Years)")]
    public int? UsefulLifeYears { get; init; }

    [Display(Name = "Warranty Expiry")]
    public DateTime? WarrantyExpiry { get; init; }

    [Display(Name = "Disposal Date")]
    public DateTime? DisposalDate { get; init; }

    [Display(Name = "Disposal Value")]
    public decimal? DisposalValue { get; init; }

    [Display(Name = "Remarks")]
    public string? Remarks { get; init; }
}