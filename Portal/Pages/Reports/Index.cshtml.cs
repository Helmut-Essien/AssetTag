using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Services;
using System.Text.Json;

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

        public async Task OnGetAsync(string reportType = "assets-by-status")
        {
            SelectedReportType = reportType;
            await LoadReportAsync(reportType);

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

                // Build CSV
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report");
                ErrorMessage = $"Export failed: {ex.Message}";
                return Page();
            }
        }

        private async Task LoadReportAsync(string reportType)
        {
            try
            {
                ReportResults = await _reportsService.GetReportAsync(reportType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading report");
                ErrorMessage = $"Failed to load report: {ex.Message}";
            }
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