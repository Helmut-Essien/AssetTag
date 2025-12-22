//namespace Shared.DTOs
//{

//    public record DashboardStatsDTO
//    {
//        public int TotalAssets { get; init; }
//        public decimal TotalValue { get; init; }
//        public int MaintenanceCount { get; init; }
//        public int RecentActivitiesCount { get; init; }
//        public Dictionary<string, int> StatusDistribution { get; init; } = new();
//        public Dictionary<string, int> ConditionDistribution { get; init; } = new();
//        public List<AssetHistoryReadDTO> RecentActivities { get; init; } = new();
//        public List<CategoryReadDTO> TopCategories { get; init; } = new();
//    }

//    public record StatusDistributionDTO
//    {
//        public string Status { get; init; } = string.Empty;
//        public int Count { get; init; }
//        public decimal Percentage { get; init; }
//    }

//    public record ConditionDistributionDTO
//    {
//        public string Condition { get; init; } = string.Empty;
//        public int Count { get; init; }
//        public decimal Percentage { get; init; }
//    }


//}

namespace Shared.DTOs;

// Chart Data DTOs
public class AssetStatusChartData
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class AssetConditionChartData
{
    public string Condition { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class MonthlyValueData
{
    public string Month { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public int AssetCount { get; set; }
    public decimal Depreciation { get; set; }
}

// Dashboard Response DTOs
public class DashboardDataDTO
{
    // Statistics
    public int TotalAssets { get; set; }
    public int AvailableAssets { get; set; }
    public int InUseAssets { get; set; }
    public int UnderMaintenanceAssets { get; set; }
    public int RetiredAssets { get; set; }
    public int LostAssets { get; set; }
    public decimal TotalAssetValue { get; set; }
    public decimal MonthlyDepreciation { get; set; }
    public int RecentActivities { get; set; }
    public int AssetsDueForMaintenance { get; set; }
    public int WarrantyExpiringSoon { get; set; }

    // Chart Data
    public List<AssetStatusChartData> StatusChartData { get; set; } = new();
    public List<AssetConditionChartData> ConditionChartData { get; set; } = new();
    public List<MonthlyValueData> MonthlyValueData { get; set; } = new();

    // Recent Activities
    public List<AssetHistoryReadDTO> RecentAssetHistories { get; set; } = new();

    // Quick Stats
    public int TotalCategories { get; set; }
    public int TotalLocations { get; set; }
    public int TotalDepartments { get; set; }
    public int TotalUsers { get; set; }

    // Performance Metrics
    public double DataLoadTimeMs { get; set; }
    public DateTime Timestamp { get; set; }
}

public class AssetSummaryDTO
{
    public string AssetId { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Condition { get; set; }
    public decimal? CurrentValue { get; set; }
    public decimal? DepreciationRate { get; set; }
    public DateTime? WarrantyExpiry { get; set; }
    public string? CategoryId { get; set; }
}

public class QuickStatsDTO
{
    public int TotalAssets { get; set; }
    public int AvailableAssets { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime LastUpdated { get; set; }
}