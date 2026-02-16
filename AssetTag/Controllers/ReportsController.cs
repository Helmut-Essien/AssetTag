using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AssetTag.Data;
using Shared.Models;
using Shared.DTOs;
using Shared.Constants;
using AssetTag.Services;

namespace AssetTag.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReportsController> _logger;
    private readonly IAIQueryService _aiQueryService;

    public ReportsController(
        ApplicationDbContext context,
        ILogger<ReportsController> logger,
        IAIQueryService aiQueryService)
    {
        _context = context;
        _logger = logger;
        _aiQueryService = aiQueryService;
    }

    [HttpGet("assets-by-status")]
    public async Task<IActionResult> GetAssetsByStatus()
    {
        try
        {
            // Load all assets with related data first
            var assets = await _context.Assets
                .Include(a => a.Category)
                .ToListAsync();

            if (!assets.Any())
            {
                return Ok(new List<AssetsByStatusDto>());
            }

            var totalAssets = assets.Count;

            // Group and calculate in memory (NetBookValue is a computed property)
            var results = assets
                .GroupBy(a => a.Status ?? "Unknown")
                .Select(g => new AssetsByStatusDto
                {
                    Status = g.Key,
                    Count = g.Count(),
                    TotalValue = g.Sum(a => a.NetBookValue ?? 0),
                    AverageValue = g.Any() ? g.Average(a => a.NetBookValue ?? 0) : 0,
                    Percentage = (decimal)g.Count() / totalAssets * 100
                })
                .OrderByDescending(r => r.Count)
                .ToList();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assets by status");
            return StatusCode(500, new { error = "Failed to generate report" });
        }
    }

    [HttpGet("assets-by-department")]
    public async Task<IActionResult> GetAssetsByDepartment()
    {
        try
        {
            // Fetch all assets with department info first
            var assets = await _context.Assets
                .Include(a => a.Department)
                .ToListAsync();

            // Group and calculate in memory
            var results = assets
                .GroupBy(a => a.Department != null ? a.Department.Name : "Unassigned")
                .Select(g => new AssetsByDepartmentDto
                {
                    Department = g.Key,
                    AssetCount = g.Count(),
                    TotalValue = g.Sum(a => a.NetBookValue ?? 0),
                    InUseCount = g.Count(a => a.Status == AssetConstants.Status.InUse),
                    AvailableCount = g.Count(a => a.Status == AssetConstants.Status.Available),
                    MaintenanceCount = g.Count(a => a.Status == AssetConstants.Status.UnderMaintenance),
                    DisposedCount = g.Count(a => a.Status == AssetConstants.Status.Disposed),
                    RetiredCount = g.Count(a => a.Status == AssetConstants.Status.Retired),
                    OtherCount = g.Count(a => a.Status != AssetConstants.Status.InUse
                        && a.Status != AssetConstants.Status.Available
                        && a.Status != AssetConstants.Status.UnderMaintenance
                        && a.Status != AssetConstants.Status.Disposed
                        && a.Status != AssetConstants.Status.Retired)
                })
                .OrderByDescending(r => r.TotalValue)
                .ToList();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assets by department");
            return StatusCode(500, new { error = "Failed to generate report" });
        }
    }

    [HttpGet("assets-by-location")]
    public async Task<IActionResult> GetAssetsByLocation()
    {
        try
        {
            // Fetch all assets with location and category info first
            var assets = await _context.Assets
                .Include(a => a.Location)
                .Include(a => a.Category)
                .ToListAsync();

            // Group and calculate in memory
            var results = assets
                .GroupBy(a => a.LocationId)
                .Select(g => new AssetsByLocationDto
                {
                    Location = g.First().Location != null
                        ? $"{g.First().Location.Name} ({g.First().Location.Campus})"
                        : "Unassigned",
                    AssetCount = g.Count(),
                    TotalValue = g.Sum(a => a.NetBookValue ?? 0),
                    AssetTypes = g.Select(a => a.Category != null ? a.Category.Name : "Unknown")
                                  .Distinct()
                                  .Take(10)
                                  .ToList()
                })
                .OrderByDescending(r => r.AssetCount)
                .ToList();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assets by location");
            return StatusCode(500, new { error = "Failed to generate report" });
        }
    }

    [HttpGet("maintenance-schedule")]
    public async Task<IActionResult> GetMaintenanceSchedule()
    {
        try
        {
            var assets = await _context.Assets
                .Include(a => a.AssetHistories)
                .Include(a => a.Category)
                .Include(a => a.Department)
                .Include(a => a.Location)
                .Where(a => (a.Status == AssetConstants.Status.InUse || a.Status == AssetConstants.Status.UnderMaintenance) &&
                    AssetConstants.Condition.RequiresMaintenance.Contains(a.Condition))
                .ToListAsync();

            var results = assets.Select(a =>
            {
                var lastMaintenance = a.AssetHistories
                    .Where(h => h.Action == AssetConstants.HistoryAction.Maintenance)
                    .OrderByDescending(h => h.Timestamp)
                    .FirstOrDefault();

                var maintenanceInterval = AssetConstants.Reports.DefaultMaintenanceIntervalMonths;
                var nextDue = lastMaintenance?.Timestamp.AddMonths(maintenanceInterval);
                var daysOverdue = nextDue.HasValue && nextDue.Value < DateTime.UtcNow
                    ? (DateTime.UtcNow - nextDue.Value).Days
                    : 0;

                var priority = daysOverdue > 30 ? "Critical" :
                              daysOverdue > 0 ? "High" :
                              nextDue.HasValue && (nextDue.Value - DateTime.UtcNow).Days <= 30 ? "Medium" : "Low";

                return new MaintenanceScheduleDto
                {
                    AssetTag = a.AssetTag,
                    Name = a.Name,
                    Condition = a.Condition,
                    Status = a.Status,
                    LastMaintenance = lastMaintenance?.Timestamp,
                    NextMaintenanceDue = nextDue,
                    DaysOverdue = daysOverdue,
                    Priority = priority,
                    Category = a.Category?.Name ?? "Unknown",
                    Department = a.Department?.Name ?? "Unassigned",
                    Location = a.Location?.Name ?? "Unknown"
                };
            })
            .OrderByDescending(r => r.DaysOverdue)
            .ThenBy(r => r.Condition)
            .ToList();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting maintenance schedule");
            return StatusCode(500, new { error = "Failed to generate report" });
        }
    }

    [HttpGet("warranty-expiry")]
    public async Task<IActionResult> GetWarrantyExpiryReport()
    {
        try
        {
            var today = DateTime.UtcNow;
            var thirtyDays = today.AddDays(AssetConstants.Reports.WarrantyExpiryCriticalDays);
            var sixtyDays = today.AddDays(AssetConstants.Reports.WarrantyExpiryHighDays);
            var ninetyDays = today.AddDays(AssetConstants.Reports.WarrantyExpiryWarningDays);

            // Fetch assets first
            var assets = await _context.Assets
                .Include(a => a.Category)
                .Include(a => a.Department)
                .Where(a => a.WarrantyExpiry.HasValue &&
                    a.WarrantyExpiry.Value >= today)
                .ToListAsync();

            // Process in memory
            var results = assets
                .Select(a => new WarrantyExpiryDto
                {
                    AssetTag = a.AssetTag,
                    Name = a.Name,
                    WarrantyExpiry = a.WarrantyExpiry!.Value,
                    DaysUntilExpiry = (int)(a.WarrantyExpiry.Value - today).TotalDays,
                    CurrentValue = a.NetBookValue,
                    Category = a.Category != null ? a.Category.Name : "Unknown",
                    Department = a.Department != null ? a.Department.Name : "Unassigned",
                    Status = a.Status,
                    Priority = a.WarrantyExpiry.Value <= thirtyDays ? "Critical" :
                              a.WarrantyExpiry.Value <= sixtyDays ? "High" :
                              a.WarrantyExpiry.Value <= ninetyDays ? "Medium" : "Low",
                    EstimatedReplacementCost = a.PurchasePrice ?? a.NetBookValue
                })
                .OrderBy(a => a.WarrantyExpiry)
                .ToList();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting warranty expiry report");
            return StatusCode(500, new { error = "Failed to generate report" });
        }
    }




    [HttpGet("depreciation-date-range")]
    public async Task<IActionResult> GetDepreciationByDateRange(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? categoryId = null,
        [FromQuery] string? departmentId = null,
        [FromQuery] string? status = null)
    {
        try
        {
            // Default to current month if dates not provided
            var start = startDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var end = endDate ?? DateTime.UtcNow;

            // Validate date range
            if (start > end)
            {
                return BadRequest(new { error = "Start date must be before end date" });
            }

            // Build query with filters
            var query = _context.Assets
                .Include(a => a.Category)
                .Include(a => a.Department)
                .Where(a => a.PurchasePrice.HasValue &&
                           a.PurchaseDate.HasValue &&
                           a.Category != null &&
                           a.Category.DepreciationRate.HasValue &&
                           a.PurchaseDate.Value <= end); // Asset must be purchased before or during the period

            // Apply filters
            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                query = query.Where(a => a.CategoryId == categoryId);
            }

            if (!string.IsNullOrWhiteSpace(departmentId))
            {
                query = query.Where(a => a.DepartmentId == departmentId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            var assets = await query.ToListAsync();

            if (!assets.Any())
            {
                return Ok(new List<DateRangeDepreciationReportDto>());
            }

            var daysInPeriod = (end - start).Days + 1;
            var results = new List<DateRangeDepreciationReportDto>();

            foreach (var asset in assets)
            {
                var purchasePrice = asset.PurchasePrice!.Value;
                var depreciationRate = asset.Category!.DepreciationRate!.Value / 100m;
                var annualDepreciation = purchasePrice * depreciationRate;
                var dailyDepreciation = annualDepreciation / 365.25m;

                // Calculate accumulated depreciation up to start of period
                var daysOwnedAtStart = asset.PurchaseDate!.Value < start
                    ? (start - asset.PurchaseDate.Value).Days
                    : 0;
                var accumulatedDepAtStart = Math.Min(dailyDepreciation * daysOwnedAtStart, purchasePrice);

                // Calculate accumulated depreciation up to end of period
                var daysOwnedAtEnd = (end - asset.PurchaseDate.Value).Days;
                var accumulatedDepAtEnd = Math.Min(dailyDepreciation * daysOwnedAtEnd, purchasePrice);

                // Depreciation for this specific period
                var depreciationForPeriod = accumulatedDepAtEnd - accumulatedDepAtStart;

                // Values at start and end of period
                var valueAtStart = purchasePrice - accumulatedDepAtStart;
                var valueAtEnd = purchasePrice - accumulatedDepAtEnd;

                results.Add(new DateRangeDepreciationReportDto
                {
                    AssetTag = asset.AssetTag,
                    Name = asset.Name,
                    Category = asset.Category.Name,
                    Department = asset.Department?.Name ?? "Unassigned",
                    PurchaseDate = asset.PurchaseDate,
                    PurchasePrice = purchasePrice,
                    DepreciationRate = asset.Category.DepreciationRate.Value,
                    CurrentValue = asset.NetBookValue ?? 0,
                    DepreciationForPeriod = depreciationForPeriod,
                    ValueAtStartOfPeriod = valueAtStart,
                    ValueAtEndOfPeriod = valueAtEnd,
                    DaysInPeriod = daysInPeriod,
                    AccumulatedDepreciation = accumulatedDepAtEnd,
                    Status = asset.Status
                });
            }

            // Order by depreciation amount (highest first)
            results = results.OrderByDescending(r => r.DepreciationForPeriod).ToList();

            return Ok(new
            {
                startDate = start,
                endDate = end,
                daysInPeriod = daysInPeriod,
                totalAssets = results.Count,
                totalDepreciationForPeriod = results.Sum(r => r.DepreciationForPeriod),
                totalValueAtStart = results.Sum(r => r.ValueAtStartOfPeriod),
                totalValueAtEnd = results.Sum(r => r.ValueAtEndOfPeriod),
                assets = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting depreciation by date range");
            return StatusCode(500, new { error = "Failed to generate depreciation report" });
        }
    }

    [HttpGet("fixed-assets-schedule")]
    public async Task<IActionResult> GetFixedAssetsSchedule([FromQuery] int? year = null)
    {
        try
        {
            // Default to current year if not specified
            var reportYear = year ?? DateTime.UtcNow.Year;
            
            // Get all categories with depreciation rates
            var categories = await _context.Categories
                .Where(c => c.DepreciationRate.HasValue)
                .OrderBy(c => c.Name)
                .ToListAsync();

            if (!categories.Any())
            {
                return Ok(new {
                    categories = new List<CategoryColumnDto>(),
                    rows = new List<FixedAssetsScheduleDto>(),
                    year = reportYear
                });
            }

            // Get all assets with their categories
            var assets = await _context.Assets
                .Include(a => a.Category)
                .Include(a => a.AssetHistories)
                .Where(a => a.Category != null &&
                           a.Category.DepreciationRate.HasValue &&
                           a.PurchasePrice.HasValue)
                .ToListAsync();

            // Build category columns
            var categoryColumns = categories.Select(c => new CategoryColumnDto
            {
                CategoryId = c.CategoryId,
                CategoryName = c.Name,
                DepreciationRate = c.DepreciationRate
            }).ToList();

            // Group assets by category
            var assetsByCategory = assets.GroupBy(a => a.CategoryId).ToDictionary(g => g.Key, g => g.ToList());

            // Build rows
            var rows = new List<FixedAssetsScheduleDto>();

            // Define date ranges for the report year
            var startOfYear = new DateTime(reportYear, 1, 1);
            var endOfYear = new DateTime(reportYear, 12, 31, 23, 59, 59);

            // Section Header: Cost/Valuation
            rows.Add(new FixedAssetsScheduleDto
            {
                RowLabel = "Cost/Valuation",
                CategoryValues = new Dictionary<string, decimal?>()
            });

            // Row 1: Cost/Valuation - Balance at start of year
            var balStartRow = new FixedAssetsScheduleDto
            {
                RowLabel = $"Bal: 1 Jan., {reportYear}",
                CategoryValues = new Dictionary<string, decimal?>()
            };
            foreach (var cat in categories)
            {
                var catAssets = assetsByCategory.ContainsKey(cat.CategoryId) ? assetsByCategory[cat.CategoryId] : new List<Asset>();
                var value = catAssets
                    .Where(a => a.PurchaseDate.HasValue && a.PurchaseDate.Value < startOfYear)
                    .Sum(a => a.PurchasePrice ?? 0);
                balStartRow.CategoryValues[cat.CategoryId] = value > 0 ? value : null;
            }
            balStartRow.Total = balStartRow.CategoryValues.Values.Sum(v => v ?? 0);
            rows.Add(balStartRow);

            // Row 2: Additions during the year
            var additionsRow = new FixedAssetsScheduleDto
            {
                RowLabel = "Additions",
                CategoryValues = new Dictionary<string, decimal?>()
            };
            foreach (var cat in categories)
            {
                var catAssets = assetsByCategory.ContainsKey(cat.CategoryId) ? assetsByCategory[cat.CategoryId] : new List<Asset>();
                var value = catAssets
                    .Where(a => a.PurchaseDate.HasValue &&
                               a.PurchaseDate.Value >= startOfYear &&
                               a.PurchaseDate.Value <= endOfYear)
                    .Sum(a => a.PurchasePrice ?? 0);
                additionsRow.CategoryValues[cat.CategoryId] = value > 0 ? value : null;
            }
            additionsRow.Total = additionsRow.CategoryValues.Values.Sum(v => v ?? 0);
            rows.Add(additionsRow);

            // Row 3: Balance at end of year
            var balEndRow = new FixedAssetsScheduleDto
            {
                RowLabel = $"Bal: 31 Dec., {reportYear}",
                CategoryValues = new Dictionary<string, decimal?>()
            };
            foreach (var cat in categories)
            {
                var catAssets = assetsByCategory.ContainsKey(cat.CategoryId) ? assetsByCategory[cat.CategoryId] : new List<Asset>();
                var value = catAssets
                    .Where(a => a.PurchaseDate.HasValue && a.PurchaseDate.Value <= endOfYear)
                    .Sum(a => a.PurchasePrice ?? 0);
                balEndRow.CategoryValues[cat.CategoryId] = value > 0 ? value : null;
            }
            balEndRow.Total = balEndRow.CategoryValues.Values.Sum(v => v ?? 0);
            rows.Add(balEndRow);

            // Empty row separator
            rows.Add(new FixedAssetsScheduleDto { RowLabel = "", CategoryValues = new Dictionary<string, decimal?>() });

            // Section Header: Depreciation
            rows.Add(new FixedAssetsScheduleDto
            {
                RowLabel = "Depreciation",
                CategoryValues = new Dictionary<string, decimal?>()
            });

            // Row 4: Depreciation - Balance at start of year
            var depBalStartRow = new FixedAssetsScheduleDto
            {
                RowLabel = $"Bal: 1 Jan., {reportYear}",
                CategoryValues = new Dictionary<string, decimal?>()
            };
            foreach (var cat in categories)
            {
                var catAssets = assetsByCategory.ContainsKey(cat.CategoryId) ? assetsByCategory[cat.CategoryId] : new List<Asset>();
                
                decimal totalDepreciation = 0;
                foreach (var asset in catAssets.Where(a => a.PurchaseDate.HasValue && a.PurchaseDate.Value < startOfYear))
                {
                    var yearsOwned = (startOfYear - asset.PurchaseDate!.Value).TotalDays / 365.25;
                    var rate = cat.DepreciationRate!.Value / 100m;
                    var depreciation = Math.Min(asset.PurchasePrice!.Value * rate * (decimal)yearsOwned, asset.PurchasePrice.Value);
                    totalDepreciation += depreciation;
                }
                
                depBalStartRow.CategoryValues[cat.CategoryId] = totalDepreciation > 0 ? totalDepreciation : null;
            }
            depBalStartRow.Total = depBalStartRow.CategoryValues.Values.Sum(v => v ?? 0);
            rows.Add(depBalStartRow);

            // Row 5: Charge for the year
            var chargeRow = new FixedAssetsScheduleDto
            {
                RowLabel = "Charge for the yr",
                CategoryValues = new Dictionary<string, decimal?>()
            };
            foreach (var cat in categories)
            {
                var catAssets = assetsByCategory.ContainsKey(cat.CategoryId) ? assetsByCategory[cat.CategoryId] : new List<Asset>();
                var rate = cat.DepreciationRate!.Value / 100m;
                
                decimal yearlyCharge = 0;
                foreach (var asset in catAssets.Where(a => a.PurchasePrice.HasValue && a.PurchaseDate.HasValue && a.PurchaseDate.Value <= endOfYear))
                {
                    // Only charge depreciation for assets owned during the year
                    yearlyCharge += asset.PurchasePrice!.Value * rate;
                }
                
                chargeRow.CategoryValues[cat.CategoryId] = yearlyCharge > 0 ? yearlyCharge : null;
            }
            chargeRow.Total = chargeRow.CategoryValues.Values.Sum(v => v ?? 0);
            rows.Add(chargeRow);

            // Row 6: Depreciation balance at end of year
            var depBalEndRow = new FixedAssetsScheduleDto
            {
                RowLabel = $"Bal: 31 Dec., {reportYear}",
                CategoryValues = new Dictionary<string, decimal?>()
            };
            foreach (var cat in categories)
            {
                var catAssets = assetsByCategory.ContainsKey(cat.CategoryId) ? assetsByCategory[cat.CategoryId] : new List<Asset>();
                
                decimal totalDepreciation = 0;
                foreach (var asset in catAssets.Where(a => a.PurchaseDate.HasValue && a.PurchaseDate.Value <= endOfYear))
                {
                    var yearsOwned = (endOfYear - asset.PurchaseDate!.Value).TotalDays / 365.25;
                    var rate = cat.DepreciationRate!.Value / 100m;
                    var depreciation = Math.Min(asset.PurchasePrice!.Value * rate * (decimal)yearsOwned, asset.PurchasePrice.Value);
                    totalDepreciation += depreciation;
                }
                
                depBalEndRow.CategoryValues[cat.CategoryId] = totalDepreciation > 0 ? totalDepreciation : null;
            }
            depBalEndRow.Total = depBalEndRow.CategoryValues.Values.Sum(v => v ?? 0);
            rows.Add(depBalEndRow);

            // Empty row separator
            rows.Add(new FixedAssetsScheduleDto { RowLabel = "", CategoryValues = new Dictionary<string, decimal?>() });

            // Row 7: Net Book Value at end of year
            var nbvRow = new FixedAssetsScheduleDto
            {
                RowLabel = $"NBV 31 Dec., {reportYear}",
                CategoryValues = new Dictionary<string, decimal?>()
            };
            foreach (var cat in categories)
            {
                var costValue = balEndRow.CategoryValues.ContainsKey(cat.CategoryId) ? balEndRow.CategoryValues[cat.CategoryId] ?? 0 : 0;
                var depValue = depBalEndRow.CategoryValues.ContainsKey(cat.CategoryId) ? depBalEndRow.CategoryValues[cat.CategoryId] ?? 0 : 0;
                var nbv = costValue - depValue;
                nbvRow.CategoryValues[cat.CategoryId] = nbv > 0 ? nbv : null;
            }
            nbvRow.Total = nbvRow.CategoryValues.Values.Sum(v => v ?? 0);
            rows.Add(nbvRow);

            return Ok(new
            {
                categories = categoryColumns,
                rows = rows,
                year = reportYear
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fixed assets schedule");
            return StatusCode(500, new { error = "Failed to generate fixed assets schedule" });
        }
    }

    [HttpGet("asset-audit-trail")]
    public async Task<IActionResult> GetAssetAuditTrail([FromQuery] string? assetId, [FromQuery] int days = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);

            var query = _context.AssetHistories
                .Include(h => h.Asset)
                .Include(h => h.User)
                .Where(h => h.Timestamp >= cutoffDate);

            if (!string.IsNullOrWhiteSpace(assetId))
            {
                query = query.Where(h => h.AssetId == assetId);
            }

            var results = await query
                .Select(h => new AssetAuditTrailDto
                {
                    HistoryId = h.HistoryId,
                    AssetId = h.AssetId,
                    AssetName = h.Asset != null ? h.Asset.Name : "Unknown",
                    Action = h.Action,
                    Description = h.Description,
                    Timestamp = h.Timestamp,
                    UserName = h.User != null ? $"{h.User.FirstName} {h.User.Surname}" : "System",
                    UserEmail = h.User != null ? h.User.Email : null
                })
                .OrderByDescending(h => h.Timestamp)
                .Take(100)
                .ToListAsync();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset audit trail");
            return StatusCode(500, new { error = "Failed to generate audit trail" });
        }
    }

    [HttpPost("ai/generate-query")]
    public async Task<IActionResult> GenerateAiQuery([FromBody] AiQueryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(new { error = "Question is required" });
            }

            _logger.LogInformation($"Generating AI query for: {request.Question}");

            var sqlQuery = await _aiQueryService.GenerateSqlFromNaturalLanguage(request.Question);

            return Ok(new
            {
                sqlQuery,
                question = request.Question,
                generatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI query");
            return StatusCode(500, new { error = "Failed to generate SQL query", details = ex.Message });
        }
    }

    [HttpPost("ai/execute-query")]
    public async Task<IActionResult> ExecuteAiQuery([FromBody] AiQueryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(new { error = "Question is required" });
            }

            _logger.LogInformation($"Executing AI query for: {request.Question}");

            var result = await _aiQueryService.ProcessNaturalLanguageQuery(request.Question);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing AI query");
            return StatusCode(500, new { error = "Failed to execute query", details = ex.Message });
        }
    }

    [HttpPost("ai/execute-sql")]
    public async Task<IActionResult> ExecuteSql([FromBody] ExecuteSqlRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SqlQuery))
            {
                return BadRequest(new { error = "SQL query is required" });
            }

            _logger.LogInformation($"Executing SQL query (length: {request.SqlQuery.Length})");

            var results = await _aiQueryService.ExecuteSafeQuery(request.SqlQuery);

            return Ok(new
            {
                results,
                rowCount = results.Count,
                sqlQuery = request.SqlQuery,
                executedAt = DateTime.UtcNow
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("dangerous"))
        {
            _logger.LogWarning($"Attempted to execute dangerous SQL: {request.SqlQuery}");
            return BadRequest(new { error = "Query contains potentially dangerous operations" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL");
            return StatusCode(500, new { error = "Failed to execute SQL", details = ex.Message });
        }
    }

    [HttpGet("ai/test-connection")]
    public async Task<IActionResult> TestAiConnection()
    {
        try
        {
            var isConnected = await _aiQueryService.TestGroqConnection();

            return Ok(new
            {
                connected = isConnected,
                service = "Groq AI",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing AI connection");
            return StatusCode(500, new { error = "Failed to test AI connection", details = ex.Message });
        }
    }

    [HttpGet("export/{reportType}")]
    public async Task<IActionResult> ExportReport(string reportType, [FromQuery] string format = "csv")
    {
        try
        {
            object? data = reportType.ToLower() switch
            {
                "assets-by-status" => await _context.Assets
                    .GroupBy(a => a.Status ?? "Unknown")
                    .Select(g => new
                    {
                        Status = g.Key,
                        Count = g.Count(),
                        TotalValue = g.Sum(a => a.NetBookValue ?? 0)
                    })
                    .ToListAsync(),
                "assets-by-department" => await _context.Assets
                    .Include(a => a.Department)
                    .GroupBy(a => a.Department != null ? a.Department.Name : "Unassigned")
                    .Select(g => new
                    {
                        Department = g.Key,
                        AssetCount = g.Count(),
                        TotalValue = g.Sum(a => a.NetBookValue ?? 0)
                    })
                    .ToListAsync(),
                _ => null
            };

            if (data == null)
            {
                return NotFound(new { error = $"Report type '{reportType}' not found" });
            }

            if (format.ToLower() == "csv")
            {
                // Simple CSV conversion
                var csvContent = ConvertToCsv(data);
                var fileName = $"{reportType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                return File(System.Text.Encoding.UTF8.GetBytes(csvContent),
                    "text/csv", fileName);
            }

            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error exporting report: {reportType}");
            return StatusCode(500, new { error = "Failed to export report" });
        }
    }

    private string ConvertToCsv(object data)
    {
        // Simple CSV conversion - in production, use a library like CsvHelper
        if (data is IEnumerable<dynamic> enumerable)
        {
            var items = enumerable.Cast<object>().ToList();
            if (!items.Any()) return "";

            var properties = items.First().GetType().GetProperties();
            var header = string.Join(",", properties.Select(p => p.Name));
            var rows = items.Select(item =>
                string.Join(",", properties.Select(p =>
                    $"\"{p.GetValue(item)?.ToString()?.Replace("\"", "\"\"")}\"")));

            return header + "\n" + string.Join("\n", rows);
        }

        return "";
    }
}

public class AiQueryRequest
{
    public string Question { get; set; } = string.Empty;
}

public class ExecuteSqlRequest
{
    public string SqlQuery { get; set; } = string.Empty;
}