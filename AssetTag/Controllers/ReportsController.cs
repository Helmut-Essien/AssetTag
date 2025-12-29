using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AssetTag.Data;
using Shared.Models;
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
            var results = await _context.Assets
                .GroupBy(a => a.Status ?? "Unknown")
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count(),
                    TotalValue = g.Sum(a => a.CurrentValue ?? 0),
                    AverageValue = g.Average(a => a.CurrentValue ?? 0)
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
                .Select(g => new
                {
                    Department = g.Key,
                    AssetCount = g.Count(),
                    TotalValue = g.Sum(a => a.CurrentValue ?? 0),
                    InUseCount = g.Count(a => a.Status == "In Use"),
                    AvailableCount = g.Count(a => a.Status == "Available"),
                    MaintenanceCount = g.Count(a => a.Status == "Under Maintenance")
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
                .GroupBy(a => a.Location != null ?
                    $"{a.Location.Name} ({a.Location.Campus})" : "Unassigned")
                .Select(g => new
                {
                    Location = g.Key,
                    AssetCount = g.Count(),
                    TotalValue = g.Sum(a => a.CurrentValue ?? 0),
                    AssetTypes = g.Select(a => a.Category != null ? a.Category.Name : "Unknown")
                                  .Distinct()
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
            var results = await _context.Assets
                .Where(a => (a.Status == "In Use" || a.Status == "Under Maintenance") &&
                    (a.Condition == "Fair" || a.Condition == "Poor" || a.Condition == "Broken"))
                .Select(a => new
                {
                    a.AssetTag,
                    a.Name,
                    a.Condition,
                    a.Status,
                    LastMaintenance = a.AssetHistories
                        .Where(h => h.Action == "Maintenance")
                        .OrderByDescending(h => h.Timestamp)
                        .FirstOrDefault().Timestamp,
                    NextMaintenanceDue = a.AssetHistories
                        .Where(h => h.Action == "Maintenance")
                        .OrderByDescending(h => h.Timestamp)
                        .FirstOrDefault().Timestamp.AddMonths(6), // Assume 6 months between maintenance
                    Category = a.Category != null ? a.Category.Name : "Unknown",
                    Department = a.Department != null ? a.Department.Name : "Unassigned",
                    Location = a.Location != null ? a.Location.Name : "Unknown"
                })
                .OrderBy(a => a.Condition)
                .ThenBy(a => a.LastMaintenance)
                .ToListAsync();

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
            var today = DateTime.Now;
            var ninetyDaysFromNow = today.AddDays(90);

            var results = await _context.Assets
                .Where(a => a.WarrantyExpiry.HasValue &&
                    a.WarrantyExpiry.Value >= today)
                .Select(a => new
                {
                    a.AssetTag,
                    a.Name,
                    WarrantyExpiry = a.WarrantyExpiry!.Value,
                    DaysUntilExpiry = (int)(a.WarrantyExpiry.Value - today).TotalDays,
                    a.CurrentValue,
                    Category = a.Category != null ? a.Category.Name : "Unknown",
                    Department = a.Department != null ? a.Department.Name : "Unassigned",
                    Status = a.Status,
                    IsExpiringSoon = a.WarrantyExpiry.Value <= ninetyDaysFromNow
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
            var results = await _context.Assets
                .Where(a => a.DepreciationRate.HasValue && a.CurrentValue.HasValue)
                .Select(a => new
                {
                    a.AssetTag,
                    a.Name,
                    CurrentValue = a.CurrentValue!.Value,
                    DepreciationRate = a.DepreciationRate!.Value,
                    MonthlyDepreciation = (a.CurrentValue.Value * a.DepreciationRate.Value) / 12 / 100,
                    YearlyDepreciation = (a.CurrentValue.Value * a.DepreciationRate.Value) / 100,
                    EstimatedValueIn1Year = a.CurrentValue.Value - (a.CurrentValue.Value * a.DepreciationRate.Value) / 100,
                    Category = a.Category != null ? a.Category.Name : "Unknown",
                    Department = a.Department != null ? a.Department.Name : "Unassigned",
                    PurchaseDate = a.AssetHistories
                        .Where(h => h.Action == "Purchased" || h.Action == "Added")
                        .OrderBy(h => h.Timestamp)
                        .FirstOrDefault().Timestamp
                })
                .OrderByDescending(a => a.YearlyDepreciation)
                .ToListAsync();

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
            var cutoffDate = DateTime.Now.AddDays(-days);

            var query = _context.AssetHistories
                .Include(h => h.Asset)
                .Include(h => h.User)
                .Where(h => h.Timestamp >= cutoffDate);

            if (!string.IsNullOrWhiteSpace(assetId))
            {
                query = query.Where(h => h.AssetId == assetId);
            }

            var results = await query
                .Select(h => new
                {
                    h.HistoryId,
                    h.AssetId,
                    AssetName = h.Asset != null ? h.Asset.Name : "Unknown",
                    h.Action,
                    h.Description,
                    h.Timestamp,
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