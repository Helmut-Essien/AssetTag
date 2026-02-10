//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.RazorPages;
//using Shared.DTOs;
//using System.Net;
//using System.Net.Http.Json;

//namespace Portal.Pages
//{
//    [Authorize]
//    public class IndexModel : PageModel
//    {
//        private readonly HttpClient _httpClient;
//        private readonly ILogger<IndexModel> _logger;
//        private readonly IConfiguration _configuration;

//        // Performance cache
//        private static DashboardCache _cache = new();
//        private readonly bool _enableCaching = true;
//        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

//        public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger, IConfiguration configuration)
//        {
//            _httpClient = httpClientFactory.CreateClient("AssetTagApi");
//            _logger = logger;
//            _configuration = configuration;
//            _enableCaching = _configuration.GetValue<bool>("Dashboard:EnableCaching", true);
//        }

//        // Dashboard Statistics
//        public int TotalAssets { get; set; }
//        public int AvailableAssets { get; set; }
//        public int InUseAssets { get; set; }
//        public int UnderMaintenanceAssets { get; set; }
//        public int RetiredAssets { get; set; }
//        public int LostAssets { get; set; }
//        public decimal TotalAssetValue { get; set; }
//        public decimal MonthlyDepreciation { get; set; }
//        public int RecentActivities { get; set; }
//        public int AssetsDueForMaintenance { get; set; }
//        public int WarrantyExpiringSoon { get; set; }

//        // Chart Data
//        public List<AssetStatusChartData> StatusChartData { get; set; } = new();
//        public List<AssetCategoryChartData> CategoryChartData { get; set; } = new();
//        public List<AssetConditionChartData> ConditionChartData { get; set; } = new();
//        public List<MonthlyValueData> MonthlyValueData { get; set; } = new();
//        public List<DepartmentAssetData> DepartmentAssetData { get; set; } = new();

//        // Recent Activities
//        public List<AssetHistoryReadDTO> RecentAssetHistories { get; set; } = new();

//        // Quick Stats
//        public int TotalCategories { get; set; }
//        public int TotalLocations { get; set; }
//        public int TotalDepartments { get; set; }
//        public int TotalUsers { get; set; }

//        // Performance Metrics
//        public double DataLoadTimeMs { get; set; }
//        public bool FromCache { get; set; }

//        public async Task<IActionResult> OnGetAsync()
//        {
//            var startTime = DateTime.UtcNow;

//            try
//            {
//                // Ensure tokens are valid before parallel loading
//                await EnsureValidTokensAsync();
//                await LoadDashboardDataAsync();
//                DataLoadTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
//                return Page();
//            }
//            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
//            {
//                return RedirectToPage("/Unauthorized");
//            }
//            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
//            {
//                return RedirectToPage("/Forbidden");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error loading dashboard data");
//                // Set default values to prevent page crash
//                SetDefaultData();
//                DataLoadTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
//                return Page();
//            }
//        }

//        public async Task<IActionResult> OnGetRefreshDashboardAsync()
//        {
//            try
//            {
//                // Clear cache and reload
//                _cache.Clear();
//                await LoadDashboardDataAsync();

//                return new JsonResult(new
//                {
//                    success = true,
//                    timestamp = DateTime.Now.ToString("HH:mm:ss"),
//                    totalAssets = TotalAssets,
//                    totalValue = TotalAssetValue.ToString("C"),
//                    availableAssets = AvailableAssets,
//                    fromCache = false
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error refreshing dashboard");
//                return new JsonResult(new { success = false, error = ex.Message });
//            }
//        }

//        public async Task<IActionResult> OnGetQuickStatsAsync()
//        {
//            try
//            {
//                var assetsResponse = await _httpClient.GetAsync("api/assets");
//                if (assetsResponse.IsSuccessStatusCode)
//                {
//                    var assets = await assetsResponse.Content.ReadFromJsonAsync<List<AssetReadDTO>>() ?? new List<AssetReadDTO>();
//                    var totalAssets = assets.Count;
//                    var availableAssets = assets.Count(a => a.Status == "Available");
//                    var totalValue = assets.Where(a => a.CurrentValue.HasValue).Sum(a => a.CurrentValue.Value);

//                    return new JsonResult(new
//                    {
//                        totalAssets,
//                        availableAssets,
//                        totalValue = totalValue.ToString("C"),
//                        lastUpdated = DateTime.Now.ToString("HH:mm:ss")
//                    });
//                }
//                return new JsonResult(new { error = "Failed to load data" });
//            }
//            catch (Exception ex)
//            {
//                return new JsonResult(new { error = ex.Message });
//            }
//        }

//        private async Task LoadDashboardDataAsync()
//        {
//            // Check cache first
//            if (_enableCaching && _cache.IsValid(_cacheDuration))
//            {
//                LoadFromCache();
//                FromCache = true;
//                return;
//            }

//            // Load all data in parallel for maximum performance
//            var tasks = new List<Task>
//            {
//                LoadAssetsData(),
//                LoadRecentHistories(),
//                LoadReferenceDataCounts(),
//                LoadAnalyticsData()
//            };

//            await Task.WhenAll(tasks);

//            // Update cache
//            UpdateCache();
//            FromCache = false;
//        }

//        private async Task LoadAssetsData()
//        {
//            var assetsResponse = await _httpClient.GetAsync("api/assets?fields=basic"); // Hypothetical endpoint with field selection
//            if (assetsResponse.IsSuccessStatusCode)
//            {
//                var assets = await assetsResponse.Content.ReadFromJsonAsync<List<AssetReadDTO>>() ?? new List<AssetReadDTO>();

//                ProcessAssetsData(assets);
//            }
//        }

//        private void ProcessAssetsData(List<AssetReadDTO> assets)
//        {
//            TotalAssets = assets.Count;
//            AvailableAssets = assets.Count(a => a.Status == "Available");
//            InUseAssets = assets.Count(a => a.Status == "In Use");
//            UnderMaintenanceAssets = assets.Count(a => a.Status == "Under Maintenance");
//            RetiredAssets = assets.Count(a => a.Status == "Retired");
//            LostAssets = assets.Count(a => a.Status == "Lost");
//            TotalAssetValue = assets.Where(a => a.CurrentValue.HasValue).Sum(a => a.CurrentValue.Value);

//            // Calculate monthly depreciation
//            MonthlyDepreciation = assets
//                .Where(a => a.DepreciationRate.HasValue && a.CurrentValue.HasValue)
//                .Sum(a => (a.CurrentValue.Value * a.DepreciationRate.Value) / 12 / 100);

//            // Maintenance and warranty alerts
//            var thirtyDaysFromNow = DateTime.Now.AddDays(30);
//            AssetsDueForMaintenance = assets.Count(a =>
//                a.Status == "In Use" &&
//                a.Condition is "Fair" or "Poor" or "Broken");

//            WarrantyExpiringSoon = assets.Count(a =>
//                a.WarrantyExpiry.HasValue &&
//                a.WarrantyExpiry.Value <= thirtyDaysFromNow &&
//                a.WarrantyExpiry.Value > DateTime.Now);

//            PrepareChartData(assets);
//        }

//        private void PrepareChartData(List<AssetReadDTO> assets)
//        {
//            // Status Distribution - ensure we have data
//            //StatusChartData = assets
//            //    .GroupBy(a => a.Status)
//            //    .Select(g => new AssetStatusChartData
//            //    {
//            //        Status = g.Key ?? "Unknown",
//            //        Count = g.Count(),
//            StatusChartData = assets
//                .GroupBy(a => a.Status)
//                .Select(g => new AssetStatusChartData
//                {
//                    Status = g.Key,
//                    Count = g.Count(),
//                    Percentage = TotalAssets > 0 ? (g.Count() * 100.0 / TotalAssets) : 0
//                })
//                .OrderByDescending(x => x.Count)
//                .ToList();

//            // If no status data, create default
//            if (!StatusChartData.Any())
//            {
//                StatusChartData = new List<AssetStatusChartData>
//        {
//            new AssetStatusChartData { Status = "No Data", Count = 1, Percentage = 100 }
//        };
//            }

//            // Condition Overview
//            ConditionChartData = assets
//                .GroupBy(a => a.Condition)
//                .Select(g => new AssetConditionChartData
//                {
//                    Condition = g.Key ?? "Unknown",
//                    Count = g.Count(),
//                    Percentage = TotalAssets > 0 ? (g.Count() * 100.0 / TotalAssets) : 0
//                })
//                .ToList();

//            // If no condition data, create default
//            if (!ConditionChartData.Any())
//            {
//                ConditionChartData = new List<AssetConditionChartData>
//        {
//            new AssetConditionChartData { Condition = "No Data", Count = 1, Percentage = 100 }
//        };
//            }

//            // Monthly trend data
//            MonthlyValueData = GenerateMonthlyValueData(assets);
//        }
//        private async Task LoadRecentHistories()
//        {
//            var historiesResponse = await _httpClient.GetAsync("api/assethistories?page=1&pageSize=5");
//            if (historiesResponse.IsSuccessStatusCode)
//            {
//                var paginatedResponse = await historiesResponse.Content.ReadFromJsonAsync<PaginatedResponse<AssetHistoryReadDTO>>();
//                RecentAssetHistories = paginatedResponse?.Data ?? new List<AssetHistoryReadDTO>();

//                // Get total activities count from last 30 days
//                var last30Days = DateTime.Now.AddDays(-30);
//                var countResponse = await _httpClient.GetAsync($"api/assethistories?fromDate={last30Days:yyyy-MM-dd}&pageSize=1");
//                if (countResponse.IsSuccessStatusCode)
//                {
//                    var countData = await countResponse.Content.ReadFromJsonAsync<PaginatedResponse<AssetHistoryReadDTO>>();
//                    RecentActivities = countData?.TotalCount ?? 0;
//                }
//            }
//        }

//        private async Task LoadReferenceDataCounts()
//        {
//            var categoriesTask = _httpClient.GetAsync("api/categories");
//            var locationsTask = _httpClient.GetAsync("api/locations");
//            var departmentsTask = _httpClient.GetAsync("api/departments");
//            var usersTask = _httpClient.GetAsync("api/users/count"); // Hypothetical endpoint

//            await Task.WhenAll(categoriesTask, locationsTask, departmentsTask, usersTask);

//            TotalCategories = await GetCountFromResponse(categoriesTask.Result);
//            TotalLocations = await GetCountFromResponse(locationsTask.Result);
//            TotalDepartments = await GetCountFromResponse(departmentsTask.Result);
//            TotalUsers = 0; // Default - would come from users endpoint
//        }

//        private async Task LoadAnalyticsData()
//        {
//            // Load additional analytics data in parallel
//            // This could include performance metrics, predictive analytics, etc.
//            await Task.Delay(10); // Simulate API call
//        }

//        private async Task<int> GetCountFromResponse(HttpResponseMessage response)
//        {
//            if (response?.IsSuccessStatusCode == true)
//            {
//                var data = await response.Content.ReadFromJsonAsync<List<object>>();
//                return data?.Count ?? 0;
//            }
//            return 0;
//        }

//        private List<MonthlyValueData> GenerateMonthlyValueData(List<AssetReadDTO> assets)
//        {
//            // Simulate monthly data - in production, use actual historical data
//            var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
//            var random = new Random();
//            var baseValue = TotalAssetValue;

//            return months.Select((month, index) => new MonthlyValueData
//            {
//                Month = month,
//                Value = baseValue * (0.7m + (decimal)(random.NextDouble() * 0.6)),
//                AssetCount = TotalAssets + random.Next(-15, 25),
//                Depreciation = baseValue * 0.02m * (index + 1)
//            }).ToList();
//        }

//        private async Task EnsureValidTokensAsync()
//        {
//            try
//            {
//                // Make a lightweight API call to validate/refresh tokens before heavy parallel loading
//                var response = await _httpClient.GetAsync("api/categories?pageSize=1");
//                response.EnsureSuccessStatusCode();
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Token validation failed, will retry during data load");
//            }
//        }
//        private void SetDefaultData()
//        {
//            TotalAssets = 0;
//            AvailableAssets = 0;
//            InUseAssets = 0;
//            UnderMaintenanceAssets = 0;
//            TotalAssetValue = 0;
//            RecentActivities = 0;
//            TotalCategories = 0;
//            TotalLocations = 0;
//            TotalDepartments = 0;
//            TotalUsers = 0;
//        }

//        private void LoadFromCache()
//        {
//            TotalAssets = _cache.TotalAssets;
//            AvailableAssets = _cache.AvailableAssets;
//            InUseAssets = _cache.InUseAssets;
//            UnderMaintenanceAssets = _cache.UnderMaintenanceAssets;
//            TotalAssetValue = _cache.TotalAssetValue;
//            RecentActivities = _cache.RecentActivities;
//            TotalCategories = _cache.TotalCategories;
//            TotalLocations = _cache.TotalLocations;
//            TotalDepartments = _cache.TotalDepartments;
//            StatusChartData = _cache.StatusChartData;
//            ConditionChartData = _cache.ConditionChartData;
//            MonthlyValueData = _cache.MonthlyValueData;
//            RecentAssetHistories = _cache.RecentAssetHistories;
//        }

//        private void UpdateCache()
//        {
//            _cache.TotalAssets = TotalAssets;
//            _cache.AvailableAssets = AvailableAssets;
//            _cache.InUseAssets = InUseAssets;
//            _cache.UnderMaintenanceAssets = UnderMaintenanceAssets;
//            _cache.TotalAssetValue = TotalAssetValue;
//            _cache.RecentActivities = RecentActivities;
//            _cache.TotalCategories = TotalCategories;
//            _cache.TotalLocations = TotalLocations;
//            _cache.TotalDepartments = TotalDepartments;
//            _cache.StatusChartData = StatusChartData;
//            _cache.ConditionChartData = ConditionChartData;
//            _cache.MonthlyValueData = MonthlyValueData;
//            _cache.RecentAssetHistories = RecentAssetHistories;
//            _cache.LastUpdated = DateTime.UtcNow;
//        }
//    }

//    // Data classes for charts and analytics


//    public class AssetStatusChartData
//    {
//        public string Status { get; set; } = string.Empty;
//        public int Count { get; set; }
//        public double Percentage { get; set; }
//    }

//    public class AssetCategoryChartData
//    {
//        public string CategoryId { get; set; } = string.Empty;
//        public string CategoryName { get; set; } = string.Empty;
//        public int Count { get; set; }
//        public double Percentage { get; set; }
//    }

//    public class AssetConditionChartData
//    {
//        public string Condition { get; set; } = string.Empty;
//        public int Count { get; set; }
//        public double Percentage { get; set; }
//    }

//    public class MonthlyValueData
//    {
//        public string Month { get; set; } = string.Empty;
//        public decimal Value { get; set; }
//        public int AssetCount { get; set; }
//        public decimal Depreciation { get; set; }
//    }

//    public class DepartmentAssetData
//    {
//        public string DepartmentId { get; set; } = string.Empty;
//        public string DepartmentName { get; set; } = string.Empty;
//        public int AssetCount { get; set; }
//        public decimal TotalValue { get; set; }
//    }

//    // Cache implementation
//    public class DashboardCache
//    {
//        public DateTime LastUpdated { get; set; } = DateTime.MinValue;

//        // Core statistics
//        public int TotalAssets { get; set; }
//        public int AvailableAssets { get; set; }
//        public int InUseAssets { get; set; }
//        public int UnderMaintenanceAssets { get; set; }
//        public decimal TotalAssetValue { get; set; }
//        public int RecentActivities { get; set; }
//        public int TotalCategories { get; set; }
//        public int TotalLocations { get; set; }
//        public int TotalDepartments { get; set; }

//        // Chart data
//        public List<AssetStatusChartData> StatusChartData { get; set; } = new();
//        public List<AssetConditionChartData> ConditionChartData { get; set; } = new();
//        public List<MonthlyValueData> MonthlyValueData { get; set; } = new();
//        public List<AssetHistoryReadDTO> RecentAssetHistories { get; set; } = new();

//        public bool IsValid(TimeSpan duration)
//        {
//            return DateTime.UtcNow - LastUpdated <= duration;
//        }

//        public void Clear()
//        {
//            LastUpdated = DateTime.MinValue;
//        }
//    }
//}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared.DTOs;
using System.Net;
using System.Net.Http.Json;
using AssetStatusChartData = Shared.DTOs.AssetStatusChartData;
using AssetConditionChartData = Shared.DTOs.AssetConditionChartData;
using MonthlyValueData = Shared.DTOs.MonthlyValueData;

namespace Portal.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _configuration;

        // Performance cache
        private static DashboardCache _cache = new();
        private readonly bool _enableCaching;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient("AssetTagApi");
            _logger = logger;
            _configuration = configuration;
            _enableCaching = _configuration.GetValue<bool>("Dashboard:EnableCaching", true);
        }

        // Dashboard Statistics
        public int TotalAssets { get; set; }
        public int AvailableAssets { get; set; }
        public int InUseAssets { get; set; }
        public int UnderMaintenanceAssets { get; set; }
        public int RetiredAssets { get; set; }
        public int LostAssets { get; set; }
        public decimal TotalAssetValue { get; set; }
        public decimal TotalAcquisitionCost { get; set; }
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
        public bool FromCache { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var startTime = DateTime.UtcNow;

            try
            {
                await LoadDashboardDataAsync();
                DataLoadTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                return Page();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                return RedirectToPage("/Unauthorized");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                return RedirectToPage("/Forbidden");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                SetDefaultData();
                DataLoadTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                return Page();
            }
        }

        public async Task<IActionResult> OnGetRefreshDashboardAsync()
        {
            try
            {
                // Clear cache and reload
                _cache.Clear();
                await LoadDashboardDataAsync();

                return new JsonResult(new
                {
                    success = true,
                    timestamp = DateTime.Now.ToString("HH:mm:ss"),
                    totalAssets = TotalAssets,
                    totalValue = TotalAssetValue.ToString("C"),
                    availableAssets = AvailableAssets,
                    fromCache = false
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing dashboard");
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        public async Task<IActionResult> OnGetQuickStatsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/dashboard/quick-stats");
                if (response.IsSuccessStatusCode)
                {
                    var quickStats = await response.Content.ReadFromJsonAsync<QuickStatsDTO>();
                    if (quickStats != null)
                    {
                        return new JsonResult(new
                        {
                            totalAssets = quickStats.TotalAssets,
                            availableAssets = quickStats.AvailableAssets,
                            totalValue = quickStats.TotalValue.ToString("C"),
                            lastUpdated = quickStats.LastUpdated.ToString("HH:mm:ss")
                        });
                    }
                }
                return new JsonResult(new { error = "Failed to load data" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading quick stats");
                return new JsonResult(new { error = ex.Message });
            }
        }

        private async Task LoadDashboardDataAsync()
        {
            // Check cache first
            if (_enableCaching && _cache.IsValid(_cacheDuration))
            {
                LoadFromCache();
                FromCache = true;
                _logger.LogInformation("Dashboard data loaded from cache");
                return;
            }

            // Load all data from single endpoint
            var response = await _httpClient.GetAsync("api/dashboard/data");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to load dashboard data. Status: {StatusCode}", response.StatusCode);
                SetDefaultData();
                return;
            }

            var dashboardData = await response.Content.ReadFromJsonAsync<DashboardDataDTO>();

            if (dashboardData == null)
            {
                _logger.LogWarning("Dashboard data deserialization returned null");
                SetDefaultData();
                return;
            }

            // Map data to page model properties
            MapDashboardData(dashboardData);

            // Update cache
            UpdateCache();
            FromCache = false;

            _logger.LogInformation("Dashboard data loaded from API in {LoadTime}ms", dashboardData.DataLoadTimeMs);
        }

        private void MapDashboardData(DashboardDataDTO data)
        {
            // Statistics
            TotalAssets = data.TotalAssets;
            AvailableAssets = data.AvailableAssets;
            InUseAssets = data.InUseAssets;
            UnderMaintenanceAssets = data.UnderMaintenanceAssets;
            RetiredAssets = data.RetiredAssets;
            LostAssets = data.LostAssets;
            TotalAssetValue = data.TotalAssetValue;
            TotalAcquisitionCost = data.TotalAcquisitionCost;
            MonthlyDepreciation = data.MonthlyDepreciation;
            RecentActivities = data.RecentActivities;
            AssetsDueForMaintenance = data.AssetsDueForMaintenance;
            WarrantyExpiringSoon = data.WarrantyExpiringSoon;

            // Chart Data
            StatusChartData = data.StatusChartData;
            ConditionChartData = data.ConditionChartData;
            MonthlyValueData = data.MonthlyValueData;

            // Recent Activities
            RecentAssetHistories = data.RecentAssetHistories;

            // Quick Stats
            TotalCategories = data.TotalCategories;
            TotalLocations = data.TotalLocations;
            TotalDepartments = data.TotalDepartments;
            TotalUsers = data.TotalUsers;
        }

        private void SetDefaultData()
        {
            TotalAssets = 0;
            AvailableAssets = 0;
            InUseAssets = 0;
            UnderMaintenanceAssets = 0;
            RetiredAssets = 0;
            LostAssets = 0;
            TotalAssetValue = 0;
            TotalAcquisitionCost = 0;
            MonthlyDepreciation = 0;
            RecentActivities = 0;
            AssetsDueForMaintenance = 0;
            WarrantyExpiringSoon = 0;
            TotalCategories = 0;
            TotalLocations = 0;
            TotalDepartments = 0;
            TotalUsers = 0;
            StatusChartData = new List<AssetStatusChartData>
            {
                new AssetStatusChartData { Status = "No Data", Count = 1, Percentage = 100 }
            };
            ConditionChartData = new List<AssetConditionChartData>
            {
                new AssetConditionChartData { Condition = "No Data", Count = 1, Percentage = 100 }
            };
            MonthlyValueData = new List<MonthlyValueData>();
            RecentAssetHistories = new List<AssetHistoryReadDTO>();
        }

        private void LoadFromCache()
        {
            TotalAssets = _cache.TotalAssets;
            AvailableAssets = _cache.AvailableAssets;
            InUseAssets = _cache.InUseAssets;
            UnderMaintenanceAssets = _cache.UnderMaintenanceAssets;
            RetiredAssets = _cache.RetiredAssets;
            LostAssets = _cache.LostAssets;
            TotalAssetValue = _cache.TotalAssetValue;
            TotalAcquisitionCost = _cache.TotalAcquisitionCost;
            MonthlyDepreciation = _cache.MonthlyDepreciation;
            RecentActivities = _cache.RecentActivities;
            AssetsDueForMaintenance = _cache.AssetsDueForMaintenance;
            WarrantyExpiringSoon = _cache.WarrantyExpiringSoon;
            TotalCategories = _cache.TotalCategories;
            TotalLocations = _cache.TotalLocations;
            TotalDepartments = _cache.TotalDepartments;
            TotalUsers = _cache.TotalUsers;
            StatusChartData = _cache.StatusChartData;
            ConditionChartData = _cache.ConditionChartData;
            MonthlyValueData = _cache.MonthlyValueData;
            RecentAssetHistories = _cache.RecentAssetHistories;
        }

        private void UpdateCache()
        {
            _cache.TotalAssets = TotalAssets;
            _cache.AvailableAssets = AvailableAssets;
            _cache.InUseAssets = InUseAssets;
            _cache.UnderMaintenanceAssets = UnderMaintenanceAssets;
            _cache.RetiredAssets = RetiredAssets;
            _cache.LostAssets = LostAssets;
            _cache.TotalAssetValue = TotalAssetValue;
            _cache.TotalAcquisitionCost = TotalAcquisitionCost;
            _cache.MonthlyDepreciation = MonthlyDepreciation;
            _cache.RecentActivities = RecentActivities;
            _cache.AssetsDueForMaintenance = AssetsDueForMaintenance;
            _cache.WarrantyExpiringSoon = WarrantyExpiringSoon;
            _cache.TotalCategories = TotalCategories;
            _cache.TotalLocations = TotalLocations;
            _cache.TotalDepartments = TotalDepartments;
            _cache.TotalUsers = TotalUsers;
            _cache.StatusChartData = StatusChartData;
            _cache.ConditionChartData = ConditionChartData;
            _cache.MonthlyValueData = MonthlyValueData;
            _cache.RecentAssetHistories = RecentAssetHistories;
            _cache.LastUpdated = DateTime.UtcNow;
        }
    }

    // Cache implementation
    public class DashboardCache
    {
        public DateTime LastUpdated { get; set; } = DateTime.MinValue;

        // Core statistics
        public int TotalAssets { get; set; }
        public int AvailableAssets { get; set; }
        public int InUseAssets { get; set; }
        public int UnderMaintenanceAssets { get; set; }
        public int RetiredAssets { get; set; }
        public int LostAssets { get; set; }
        public decimal TotalAssetValue { get; set; }
        public decimal TotalAcquisitionCost { get; set; }
        public decimal MonthlyDepreciation { get; set; }
        public int RecentActivities { get; set; }
        public int AssetsDueForMaintenance { get; set; }
        public int WarrantyExpiringSoon { get; set; }
        public int TotalCategories { get; set; }
        public int TotalLocations { get; set; }
        public int TotalDepartments { get; set; }
        public int TotalUsers { get; set; }

        // Chart data
        public List<AssetStatusChartData> StatusChartData { get; set; } = new();
        public List<AssetConditionChartData> ConditionChartData { get; set; } = new();
        public List<MonthlyValueData> MonthlyValueData { get; set; } = new();
        public List<AssetHistoryReadDTO> RecentAssetHistories { get; set; } = new();

        public bool IsValid(TimeSpan duration)
        {
            return DateTime.UtcNow - LastUpdated <= duration;
        }

        public void Clear()
        {
            LastUpdated = DateTime.MinValue;
        }
    }
}

