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
            var totalAssets = await _context.Assets.CountAsync();
            if (totalAssets == 0)
            {
                return Ok(new List<AssetsByStatusDto>());
            }

            var results = await _context.Assets
                .GroupBy(a => a.Status ?? "Unknown")
                .Select(g => new AssetsByStatusDto
                {
                    Status = g.Key,
                    Count = g.Count(),
                    TotalValue = g.Sum(a => a.CurrentValue ?? 0),
                    AverageValue = g.Any() ? g.Average(a => a.CurrentValue ?? 0) : 0,
                    Percentage = (decimal)g.Count() / totalAssets * 100
                })
                .OrderByDescending(r => r.Count)
                .ToListAsync();

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
            var results = await _context.Assets
                .Include(a => a.Department)
                .GroupBy(a => a.Department != null ? a.Department.Name : "Unassigned")
                .Select(g => new AssetsByDepartmentDto
                {
                    Department = g.Key,
                    AssetCount = g.Count(),
                    TotalValue = g.Sum(a => a.CurrentValue ?? 0),
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
                .ToListAsync();

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
            var results = await _context.Assets
                .Include(a => a.Location)
                .Include(a => a.Category)
                .GroupBy(a => a.LocationId)
                .Select(g => new AssetsByLocationDto
                {
                    Location = g.First().Location != null
                        ? $"{g.First().Location.Name} ({g.First().Location.Campus})"
                        : "Unassigned",
                    AssetCount = g.Count(),
                    TotalValue = g.Sum(a => a.CurrentValue ?? 0),
                    AssetTypes = g.Select(a => a.Category != null ? a.Category.Name : "Unknown")
                                  .Distinct()
                                  .Take(10)
                                  .ToList()
                })
                .OrderByDescending(r => r.AssetCount)
                .ToListAsync();

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

            var results = await _context.Assets
                .Include(a => a.Category)
                .Include(a => a.Department)
                .Where(a => a.WarrantyExpiry.HasValue &&
                    a.WarrantyExpiry.Value >= today)
                .Select(a => new WarrantyExpiryDto
                {
                    AssetTag = a.AssetTag,
                    Name = a.Name,
                    WarrantyExpiry = a.WarrantyExpiry!.Value,
                    DaysUntilExpiry = (int)(a.WarrantyExpiry.Value - today).TotalDays,
                    CurrentValue = a.CurrentValue,
                    Category = a.Category != null ? a.Category.Name : "Unknown",
                    Department = a.Department != null ? a.Department.Name : "Unassigned",
                    Status = a.Status,
                    Priority = a.WarrantyExpiry.Value <= thirtyDays ? "Critical" :
                              a.WarrantyExpiry.Value <= sixtyDays ? "High" :
                              a.WarrantyExpiry.Value <= ninetyDays ? "Medium" : "Low",
                    EstimatedReplacementCost = a.PurchasePrice ?? a.CurrentValue
                })
                .OrderBy(a => a.WarrantyExpiry)
                .ToListAsync();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting warranty expiry report");
            return StatusCode(500, new { error = "Failed to generate report" });
        }
    }

    [HttpGet("depreciation-report")]
    public async Task<IActionResult> GetDepreciationReport()
    {
        try
        {
            var assets = await _context.Assets
                .Include(a => a.Category)
                .Include(a => a.Department)
                .Include(a => a.AssetHistories)
                .Where(a => a.Category != null && a.Category.DepreciationRate.HasValue && a.CurrentValue.HasValue)
                .ToListAsync();

            var results = assets.Select(a =>
            {
                var purchaseDate = a.AssetHistories
                    .Where(h => h.Action == AssetConstants.HistoryAction.Purchased ||
                               h.Action == AssetConstants.HistoryAction.Added)
                    .OrderBy(h => h.Timestamp)
                    .FirstOrDefault()?.Timestamp;

                var ageInMonths = purchaseDate.HasValue
                    ? (int)((DateTime.UtcNow - purchaseDate.Value).TotalDays / 30.44)
                    : 0;

                var rate = a.Category!.DepreciationRate!.Value;
                var currentValue = a.CurrentValue!.Value;

                return new DepreciationReportDto
                {
                    AssetTag = a.AssetTag,
                    Name = a.Name,
                    CurrentValue = currentValue,
                    DepreciationRate = rate,
                    MonthlyDepreciation = (currentValue * rate) / 12 / 100,
                    YearlyDepreciation = (currentValue * rate) / 100,
                    EstimatedValueIn1Year = currentValue - (currentValue * rate) / 100,
                    Category = a.Category.Name,
                    Department = a.Department?.Name ?? "Unassigned",
                    PurchaseDate = purchaseDate,
                    AgeInMonths = ageInMonths,
                    AccumulatedDepreciation = a.AccumulatedDepreciation ?? 0,
                    NetBookValue = a.NetBookValue ?? currentValue
                };
            })
            .OrderByDescending(a => a.YearlyDepreciation)
            .ToList();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting depreciation report");
            return StatusCode(500, new { error = "Failed to generate report" });
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
                        TotalValue = g.Sum(a => a.CurrentValue ?? 0)
                    })
                    .ToListAsync(),
                "assets-by-department" => await _context.Assets
                    .Include(a => a.Department)
                    .GroupBy(a => a.Department != null ? a.Department.Name : "Unassigned")
                    .Select(g => new
                    {
                        Department = g.Key,
                        AssetCount = g.Count(),
                        TotalValue = g.Sum(a => a.CurrentValue ?? 0)
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