namespace Shared.DTOs
{
    /// <summary>
    /// DTO for Assets by Status report
    /// </summary>
    public class AssetsByStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalValue { get; set; }
        public decimal AverageValue { get; set; }
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// DTO for Assets by Department report
    /// </summary>
    public class AssetsByDepartmentDto
    {
        public string Department { get; set; } = string.Empty;
        public int AssetCount { get; set; }
        public decimal TotalValue { get; set; }
        public int InUseCount { get; set; }
        public int AvailableCount { get; set; }
        public int MaintenanceCount { get; set; }
        public int DisposedCount { get; set; }
        public int OtherCount { get; set; }
    }

    /// <summary>
    /// DTO for Assets by Location report
    /// </summary>
    public class AssetsByLocationDto
    {
        public string Location { get; set; } = string.Empty;
        public int AssetCount { get; set; }
        public decimal TotalValue { get; set; }
        public List<string> AssetTypes { get; set; } = new();
    }

    /// <summary>
    /// DTO for Maintenance Schedule report
    /// </summary>
    public class MaintenanceScheduleDto
    {
        public string AssetTag { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? LastMaintenance { get; set; }
        public DateTime? NextMaintenanceDue { get; set; }
        public int DaysOverdue { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for Warranty Expiry report
    /// </summary>
    public class WarrantyExpiryDto
    {
        public string AssetTag { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime WarrantyExpiry { get; set; }
        public int DaysUntilExpiry { get; set; }
        public decimal? CurrentValue { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public decimal? EstimatedReplacementCost { get; set; }
    }

    /// <summary>
    /// DTO for Depreciation report
    /// </summary>
    public class DepreciationReportDto
    {
        public string AssetTag { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal CurrentValue { get; set; }
        public decimal DepreciationRate { get; set; }
        public decimal MonthlyDepreciation { get; set; }
        public decimal YearlyDepreciation { get; set; }
        public decimal EstimatedValueIn1Year { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public DateTime? PurchaseDate { get; set; }
        public int AgeInMonths { get; set; }
        public decimal AccumulatedDepreciation { get; set; }
        public decimal NetBookValue { get; set; }
    }

    /// <summary>
    /// DTO for Date Range Depreciation report
    /// </summary>
    public class DateRangeDepreciationReportDto
    {
        public string AssetTag { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public DateTime? PurchaseDate { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal DepreciationRate { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal DepreciationForPeriod { get; set; }
        public decimal ValueAtStartOfPeriod { get; set; }
        public decimal ValueAtEndOfPeriod { get; set; }
        public int DaysInPeriod { get; set; }
        public decimal AccumulatedDepreciation { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request DTO for date range depreciation report
    /// </summary>
    public class DateRangeDepreciationRequestDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? CategoryId { get; set; }
        public string? DepartmentId { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>
    /// DTO for Asset Audit Trail report
    /// </summary>
    public class AssetAuditTrailDto
    {
        public string HistoryId { get; set; } = string.Empty;
        public string AssetId { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? UserEmail { get; set; }
    }
    /// <summary>
    /// DTO for Fixed Assets Schedule report - categories as columns
    /// </summary>
    public class FixedAssetsScheduleDto
    {
        public string RowLabel { get; set; } = string.Empty;
        public Dictionary<string, decimal?> CategoryValues { get; set; } = new();
        public decimal? Total { get; set; }
    }
    
    /// <summary>
    /// Category column header with depreciation rate
    /// </summary>
    public class CategoryColumnDto
    {
        public string CategoryId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal? DepreciationRate { get; set; }
        public string DisplayName => DepreciationRate.HasValue
            ? $"{CategoryName} ({DepreciationRate:0.##}%)"
            : CategoryName;
    }

    /// <summary>
    /// DTO for a disposed asset with gain/loss calculation
    /// </summary>
    public class DisposedAssetDto
    {
        public string AssetId { get; set; } = string.Empty;
        public string AssetTag { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public DateTime? DisposalDate { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal AccumulatedDepreciation { get; set; }
        public decimal NetBookValueAtDisposal { get; set; }
        public decimal DisposalValue { get; set; }
        public decimal GainLossOnDisposal { get; set; }
    }

    /// <summary>
    /// DTO for Fixed Assets Schedule with active assets, disposals, and summary
    /// </summary>
    public class FixedAssetsReportResponse
    {
        public List<CategoryColumnDto> Categories { get; set; } = new();
        public List<FixedAssetsScheduleDto> ActiveAssetRows { get; set; } = new();
        public DisposalsSectionDto? DisposalsSection { get; set; }
        public FixedAssetsReportSummaryDto? Summary { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    /// <summary>
    /// DTO for disposals section of the report
    /// </summary>
    public class DisposalsSectionDto
    {
        public List<DisposedAssetDto> DisposedAssets { get; set; } = new();
        public DisposalsSummaryDto Totals { get; set; } = new();
    }

    /// <summary>
    /// Totals for disposed assets section
    /// </summary>
    public class DisposalsSummaryDto
    {
        public decimal TotalPurchasePrice { get; set; }
        public decimal TotalAccumulatedDepreciation { get; set; }
        public decimal TotalNetBookValue { get; set; }
        public decimal TotalDisposalValue { get; set; }
        public decimal TotalGainLoss { get; set; }
    }

    /// <summary>
    /// Summary/reconciliation for the fixed assets report
    /// </summary>
    public class FixedAssetsReportSummaryDto
    {
        public decimal OpeningBalance { get; set; }
        public decimal Additions { get; set; }
        public decimal DepreciationCharge { get; set; }
        public decimal DisposalCost { get; set; }
        public decimal DisposalProceeds { get; set; }
        public decimal ClosingBalance { get; set; }
        public string Reconciliation { get; set; } = string.Empty; // For debugging: should equal 0
    }
}