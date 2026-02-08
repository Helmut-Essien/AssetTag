using AssetTag.Data;
using Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace AssetTag.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all dashboard data in a single request
    /// </summary>
    [HttpGet("data")]
    public async Task<ActionResult<DashboardDataDTO>> GetDashboardData()
    {
        try
        {
            var startTime = DateTime.UtcNow;

            // Load all assets with required data in a single query
            var assets = await _context.Assets
                .AsNoTracking()
                .Include(a => a.Category)
                .Select(a => new AssetSummaryDTO
                {
                    AssetId = a.AssetId,
                    Status = a.Status,
                    Condition = a.Condition,
                    CurrentValue = a.CurrentValue,
                    DepreciationRate = a.Category != null ? a.Category.DepreciationRate : null,
                    WarrantyExpiry = a.WarrantyExpiry,
                    CategoryId = a.CategoryId
                })
                .ToListAsync();

            // Load recent histories in a single query
            var recentHistories = await _context.AssetHistories
                .AsNoTracking()
                .OrderByDescending(h => h.Timestamp)
                .Take(5)
                .Include(h => h.Asset)
                .Include(h => h.User)
                .Select(h => new AssetHistoryReadDTO
                {
                    HistoryId = h.HistoryId,
                    AssetId = h.AssetId,
                    AssetName = h.Asset != null ? h.Asset.Name : "Unknown",
                    Action = h.Action,
                    Description = h.Description,
                    Timestamp = h.Timestamp,
                    UserId = h.UserId,
                    UserFullName = h.User != null ? $"{h.User.FirstName} {h.User.Surname}" : "Unknown"
                })
                .ToListAsync();

            // Load reference data counts in a single query each
            var categoriesCount = await _context.Categories.CountAsync();
            var locationsCount = await _context.Locations.CountAsync();
            var departmentsCount = await _context.Departments.CountAsync();
            var usersCount = await _context.Users.Where(u => u.IsActive).CountAsync();

            // Calculate recent activities count (last 30 days)
            var last30Days = DateTime.Now.AddDays(-30);
            var recentActivitiesCount = await _context.AssetHistories
                .Where(h => h.Timestamp >= last30Days)
                .CountAsync();

            // Process asset statistics
            var totalAssets = assets.Count;
            var availableAssets = assets.Count(a => a.Status == "Available");
            var inUseAssets = assets.Count(a => a.Status == "In Use");
            var underMaintenanceAssets = assets.Count(a => a.Status == "Under Maintenance");
            var retiredAssets = assets.Count(a => a.Status == "Retired");
            var lostAssets = assets.Count(a => a.Status == "Lost");
            var totalAssetValue = assets.Where(a => a.CurrentValue.HasValue).Sum(a => a.CurrentValue!.Value);

            // Calculate monthly depreciation
            var monthlyDepreciation = assets
                .Where(a => a.DepreciationRate.HasValue && a.CurrentValue.HasValue)
                .Sum(a => (a.CurrentValue!.Value * a.DepreciationRate!.Value) / 12 / 100);

            // Maintenance and warranty alerts
            var thirtyDaysFromNow = DateTime.Now.AddDays(30);
            var assetsDueForMaintenance = assets.Count(a =>
                a.Status == "In Use" &&
                (a.Condition == "Fair" || a.Condition == "Poor" || a.Condition == "Broken"));

            var warrantyExpiringSoon = assets.Count(a =>
                a.WarrantyExpiry.HasValue &&
                a.WarrantyExpiry.Value <= thirtyDaysFromNow &&
                a.WarrantyExpiry.Value > DateTime.Now);

            // Prepare chart data
            var statusChartData = assets
                .GroupBy(a => a.Status ?? "Unknown")
                .Select(g => new AssetStatusChartData
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Percentage = totalAssets > 0 ? (g.Count() * 100.0 / totalAssets) : 0
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            // Handle empty status data
            if (!statusChartData.Any())
            {
                statusChartData = new List<AssetStatusChartData>
                {
                    new AssetStatusChartData { Status = "No Data", Count = 1, Percentage = 100 }
                };
            }

            var conditionChartData = assets
                .GroupBy(a => a.Condition ?? "Unknown")
                .Select(g => new AssetConditionChartData
                {
                    Condition = g.Key,
                    Count = g.Count(),
                    Percentage = totalAssets > 0 ? (g.Count() * 100.0 / totalAssets) : 0
                })
                .ToList();

            // Handle empty condition data
            if (!conditionChartData.Any())
            {
                conditionChartData = new List<AssetConditionChartData>
                {
                    new AssetConditionChartData { Condition = "No Data", Count = 1, Percentage = 100 }
                };
            }

            // Generate monthly value data
            var monthlyValueData = GenerateMonthlyValueData(totalAssetValue, totalAssets);

            var loadTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            var dashboardData = new DashboardDataDTO
            {
                // Statistics
                TotalAssets = totalAssets,
                AvailableAssets = availableAssets,
                InUseAssets = inUseAssets,
                UnderMaintenanceAssets = underMaintenanceAssets,
                RetiredAssets = retiredAssets,
                LostAssets = lostAssets,
                TotalAssetValue = totalAssetValue,
                MonthlyDepreciation = monthlyDepreciation,
                RecentActivities = recentActivitiesCount,
                AssetsDueForMaintenance = assetsDueForMaintenance,
                WarrantyExpiringSoon = warrantyExpiringSoon,

                // Chart Data
                StatusChartData = statusChartData,
                ConditionChartData = conditionChartData,
                MonthlyValueData = monthlyValueData,

                // Recent Activities
                RecentAssetHistories = recentHistories,

                // Quick Stats
                TotalCategories = categoriesCount,
                TotalLocations = locationsCount,
                TotalDepartments = departmentsCount,
                TotalUsers = usersCount,

                // Performance Metrics
                DataLoadTimeMs = loadTimeMs,
                Timestamp = DateTime.UtcNow
            };

            return Ok(dashboardData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard data");
            return StatusCode(500, new { error = "An error occurred while loading dashboard data", details = ex.Message });
        }
    }

    /// <summary>
    /// Get quick stats only (lightweight endpoint)
    /// </summary>
    [HttpGet("quick-stats")]
    public async Task<ActionResult<QuickStatsDTO>> GetQuickStats()
    {
        try
        {
            var totalAssets = await _context.Assets.CountAsync();
            var availableAssets = await _context.Assets.CountAsync(a => a.Status == "Available");
            var totalValue = await _context.Assets
                .Where(a => a.CurrentValue.HasValue)
                .SumAsync(a => a.CurrentValue!.Value);

            return Ok(new QuickStatsDTO
            {
                TotalAssets = totalAssets,
                AvailableAssets = availableAssets,
                TotalValue = totalValue,
                LastUpdated = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading quick stats");
            return StatusCode(500, new { error = "An error occurred while loading quick stats", details = ex.Message });
        }
    }

    private List<MonthlyValueData> GenerateMonthlyValueData(decimal baseValue, int totalAssets)
    {
        var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        var random = new Random();

        return months.Select((month, index) => new MonthlyValueData
        {
            Month = month,
            Value = baseValue > 0 ? baseValue * (0.7m + (decimal)(random.NextDouble() * 0.6)) : 0,
            AssetCount = totalAssets + random.Next(-15, 25),
            Depreciation = baseValue > 0 ? baseValue * 0.02m * (index + 1) : 0
        }).ToList();
    }
}

// Note: All DTOs are now in Shared.DTOs namespace
// See DashboardDTOs.cs in the Shared project