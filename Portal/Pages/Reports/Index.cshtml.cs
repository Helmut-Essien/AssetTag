using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Services;
using System.Text.Json;
using ClosedXML.Excel;

namespace Portal.Pages.Reports
{
    public class IndexModel : PageModel
    {
        private readonly IReportsService _reportsService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IReportsService reportsService, ILogger<IndexModel> logger)
        {
            _reportsService = reportsService;
            _logger = logger;
        }

        // Properties for the view
        public List<Dictionary<string, object>> ReportResults { get; set; } = new();
        public string GeneratedSql { get; set; } = string.Empty;
        public string SelectedReportType { get; set; } = "assets-by-status";
        public List<ChatMessage> ChatHistory { get; set; } = new();

        // Add missing properties
        public bool IsAiConnected { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
        public int? ReportYear { get; set; }
        
        // Date range depreciation properties
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public JsonElement DepreciationSummary { get; set; }
        public List<Dictionary<string, object>> DepreciationResults { get; set; } = new();

        public async Task OnGetAsync(string reportType = "assets-by-status", int? year = null,
            DateTime? startDate = null, DateTime? endDate = null)
        {
            SelectedReportType = reportType;
            ReportYear = year;
            StartDate = startDate;
            EndDate = endDate;
            
            // Test AI connection
            IsAiConnected = await _reportsService.TestAiConnectionAsync();
            
            // Load depreciation date range report if selected
            if (reportType == "depreciation-date-range")
            {
                await LoadDepreciationDateRangeAsync(startDate, endDate);
            }
            else
            {
                await LoadReportAsync(reportType, year);
            }

            // Initialize chat history from session
            ChatHistory = HttpContext.Session.GetObject<List<ChatMessage>>("ChatHistory")
                ?? new List<ChatMessage>();

            if (!ChatHistory.Any())
            {
                AddSystemMessage("Welcome to the AI Report Assistant! Ask me questions about your assets.");
            }
        }

        public async Task<IActionResult> OnPostProcessQueryAsync(string chatInput, bool autoQuery = true)
        {
            if (string.IsNullOrEmpty(chatInput))
                return Page();

            AddUserMessage(chatInput);

            try
            {
                if (autoQuery)
                {
                    // Generate SQL only
                    GeneratedSql = await _reportsService.GenerateAiQueryAsync(chatInput);
                    AddAiMessage("I've generated a SQL query for your question. Click 'Run This Query' to execute it.");
                }
                else
                {
                    // Execute query directly
                    var results = await _reportsService.ExecuteAiQueryAsync(chatInput);
                    ReportResults = results;
                    AddAiMessage($"Found {results.Count} result(s) for your query.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat query");
                AddAiMessage($"Sorry, I couldn't process your request: {ex.Message}");
            }

            SaveChatHistory();
            return Page();
        }

        public async Task<IActionResult> OnPostRunSqlAsync(string sqlQuery)
        {
            if (string.IsNullOrEmpty(sqlQuery))
                return Page();

            try
            {
                var results = await _reportsService.ExecuteSqlAsync(sqlQuery);
                ReportResults = results;
                GeneratedSql = sqlQuery;
                AddAiMessage($"Executed SQL successfully. Found {results.Count} result(s).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running SQL query");
                AddAiMessage($"Error executing SQL: {ex.Message}");
            }

            SaveChatHistory();
            return Page();
        }

        public async Task<IActionResult> OnPostExportReportAsync(string format, string reportType)
        {
            try
            {
                var reportData = await _reportsService.GetReportAsync(reportType);

                if (!reportData.Any())
                {
                    ErrorMessage = "No data to export";
                    return Page();
                }

                if (format.ToLower() == "excel" && reportType == "fixed-assets-schedule")
                {
                    return await ExportFixedAssetsScheduleToExcel(reportData, reportType);
                }
                else if (format.ToLower() == "excel")
                {
                    return await ExportToExcel(reportData, reportType);
                }
                else
                {
                    // CSV export
                    var csv = new System.Text.StringBuilder();

                    // Header
                    var firstRow = reportData.First();
                    var header = string.Join(",", firstRow.Keys);
                    csv.AppendLine(header);

                    // Data rows
                    foreach (var row in reportData)
                    {
                        var values = row.Values.Select(v =>
                        {
                            var text = v?.ToString() ?? string.Empty;
                            // Escape quotes
                            text = text.Replace("\"", "\"\"");
                            // Wrap in quotes if contains comma, quote, or newline
                            if (text.Contains(",") || text.Contains("\"") || text.Contains("\n"))
                            {
                                text = $"\"{text}\"";
                            }
                            return text;
                        });
                        csv.AppendLine(string.Join(",", values));
                    }

                    var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                    return File(bytes, "text/csv", $"{reportType}_{DateTime.Now:yyyyMMddHHmmss}.csv");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report");
                ErrorMessage = $"Export failed: {ex.Message}";
                return Page();
            }
        }

        private async Task<IActionResult> ExportFixedAssetsScheduleToExcel(List<Dictionary<string, object>> reportData, string reportType)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Fixed Assets Schedule");

            var currentRow = 1;
            var headers = reportData.First().Keys.ToList();

            // Add title
            worksheet.Cell(currentRow, 1).Value = "FIXED ASSETS SCHEDULE";
            worksheet.Range(currentRow, 1, currentRow, headers.Count).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
            worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            currentRow += 2;

            // Add headers
            for (int i = 0; i < headers.Count; i++)
            {
                var cell = worksheet.Cell(currentRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.DarkGray;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            currentRow++;

            // Add data rows
            foreach (var row in reportData)
            {
                var rowLabel = row.Values.FirstOrDefault()?.ToString() ?? "";
                
                // Check if section header
                if (rowLabel == "Cost/Valuation" || rowLabel == "Depreciation")
                {
                    worksheet.Range(currentRow, 1, currentRow, headers.Count).Merge();
                    var cell = worksheet.Cell(currentRow, 1);
                    cell.Value = rowLabel;
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontSize = 11;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                else if (string.IsNullOrEmpty(rowLabel))
                {
                    // Empty separator row - just increment
                }
                else
                {
                    // Regular data row
                    for (int i = 0; i < headers.Count; i++)
                    {
                        var cell = worksheet.Cell(currentRow, i + 1);
                        var value = row[headers[i]];
                        
                        if (value is decimal decimalValue)
                        {
                            cell.Value = decimalValue;
                            cell.Style.NumberFormat.Format = "#,##0";
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        }
                        else if (value?.ToString() == "-")
                        {
                            cell.Value = "-";
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }
                        else
                        {
                            cell.Value = value?.ToString() ?? "";
                        }
                        
                        // Bold first column (row labels) and last column (totals)
                        if (i == 0 || i == headers.Count - 1)
                        {
                            cell.Style.Font.Bold = true;
                        }
                        
                        // Highlight NBV row
                        if (rowLabel.StartsWith("NBV"))
                        {
                            cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
                            cell.Style.Font.Bold = true;
                        }
                        
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }
                }
                
                currentRow++;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            // Save to memory stream
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{reportType}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
        }

        private async Task<IActionResult> ExportToExcel(List<Dictionary<string, object>> reportData, string reportType)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Report");

            var currentRow = 1;
            var headers = reportData.First().Keys.ToList();

            // Add headers
            for (int i = 0; i < headers.Count; i++)
            {
                var cell = worksheet.Cell(currentRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }
            currentRow++;

            // Add data
            foreach (var row in reportData)
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    var cell = worksheet.Cell(currentRow, i + 1);
                    var value = row[headers[i]];
                    
                    if (value is decimal decimalValue)
                    {
                        cell.Value = decimalValue;
                        cell.Style.NumberFormat.Format = "#,##0.00";
                    }
                    else if (value is DateTime dateValue)
                    {
                        cell.Value = dateValue;
                        cell.Style.DateFormat.Format = "yyyy-MM-dd";
                    }
                    else
                    {
                        cell.Value = value?.ToString() ?? "";
                    }
                }
                currentRow++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{reportType}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
        }

        private async Task LoadReportAsync(string reportType, int? year = null)
        {
            try
            {
                ReportResults = await _reportsService.GetReportAsync(reportType, year);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading report");
                ErrorMessage = $"Failed to load report: {ex.Message}";
            }
        }

        private async Task LoadDepreciationDateRangeAsync(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var result = await _reportsService.GetDepreciationByDateRangeAsync(startDate, endDate);
                
                if (result.ValueKind != JsonValueKind.Undefined && result.ValueKind != JsonValueKind.Null)
                {
                    DepreciationSummary = result;
                    
                    // Extract assets array and convert to list of dictionaries
                    if (result.TryGetProperty("assets", out var assetsElement) &&
                        assetsElement.ValueKind == JsonValueKind.Array)
                    {
                        DepreciationResults = new List<Dictionary<string, object>>();
                        
                        foreach (var asset in assetsElement.EnumerateArray())
                        {
                            var dict = new Dictionary<string, object>();
                            foreach (var property in asset.EnumerateObject())
                            {
                                dict[property.Name] = ConvertJsonElement(property.Value);
                            }
                            DepreciationResults.Add(dict);
                        }
                        
                        ReportResults = DepreciationResults;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading depreciation date range report");
                ErrorMessage = $"Failed to load depreciation report: {ex.Message}";
            }
        }

        private object ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal :
                                       element.TryGetDecimal(out var decVal) ? decVal :
                                       element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                _ => element.ToString()
            };
        }

        private void AddUserMessage(string message)
        {
            ChatHistory.Add(new ChatMessage
            {
                Role = "user",
                Content = message,
                Timestamp = DateTime.Now
            });
        }

        private void AddAiMessage(string message)
        {
            ChatHistory.Add(new ChatMessage
            {
                Role = "assistant",
                Content = message,
                Timestamp = DateTime.Now
            });
        }

        private void AddSystemMessage(string message)
        {
            ChatHistory.Add(new ChatMessage
            {
                Role = "system",
                Content = message,
                Timestamp = DateTime.Now
            });
        }

        private void SaveChatHistory()
        {
            if (ChatHistory.Count > 20)
            {
                ChatHistory = ChatHistory.Skip(ChatHistory.Count - 20).ToList();
            }

            HttpContext.Session.SetObject("ChatHistory", ChatHistory);
        }
    }

    public class ChatMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Add missing property used in view
        public string DisplayTime => Timestamp.ToString("HH:mm");
    }

    public static class SessionExtensions
    {
        public static void SetObject<T>(this Microsoft.AspNetCore.Http.ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        public static T? GetObject<T>(this Microsoft.AspNetCore.Http.ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : JsonSerializer.Deserialize<T>(value);
        }
    }
}