namespace Shared.DTOs
{
    public record DashboardStatsDTO
    {
        public int TotalAssets { get; init; }
        public decimal TotalValue { get; init; }
        public int MaintenanceCount { get; init; }
        public int RecentActivitiesCount { get; init; }
        public Dictionary<string, int> StatusDistribution { get; init; } = new();
        public Dictionary<string, int> ConditionDistribution { get; init; } = new();
        public List<AssetHistoryReadDTO> RecentActivities { get; init; } = new();
        public List<CategoryReadDTO> TopCategories { get; init; } = new();
    }

    public record StatusDistributionDTO
    {
        public string Status { get; init; } = string.Empty;
        public int Count { get; init; }
        public decimal Percentage { get; init; }
    }

    public record ConditionDistributionDTO
    {
        public string Condition { get; init; } = string.Empty;
        public int Count { get; init; }
        public decimal Percentage { get; init; }
    }
}