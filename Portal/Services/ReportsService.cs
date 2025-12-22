using System.Net.Http.Json;
using System.Text.Json;

namespace Portal.Services
{
    public class ReportsService : IReportsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ReportsService> _logger;

        public ReportsService(IHttpClientFactory httpClientFactory, ILogger<ReportsService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<List<Dictionary<string, object>>> GetReportAsync(string reportType)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("AssetTagApi");
                var response = await httpClient.GetAsync($"api/reports/{reportType}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content)
                        ?? new List<Dictionary<string, object>>();
                }

                _logger.LogError($"Failed to get report: {response.StatusCode}");
                return new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting report {reportType}");
                return new List<Dictionary<string, object>>();
            }
        }

        public async Task<string> GenerateAiQueryAsync(string question)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("AssetTagApi");
                var request = new { Question = question };
                var response = await httpClient.PostAsJsonAsync("api/reports/ai/generate-query", request);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    return result.GetProperty("sqlQuery").GetString() ?? string.Empty;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI query");
                return string.Empty;
            }
        }

        public async Task<List<Dictionary<string, object>>> ExecuteAiQueryAsync(string question)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("AssetTagApi");
                var request = new { Question = question };
                var response = await httpClient.PostAsJsonAsync("api/reports/ai/execute-query", request);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (result.TryGetProperty("results", out var resultsElement))
                    {
                        return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(resultsElement.GetRawText())
                            ?? new List<Dictionary<string, object>>();
                    }
                }

                return new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing AI query");
                return new List<Dictionary<string, object>>();
            }
        }

        public async Task<List<Dictionary<string, object>>> ExecuteSqlAsync(string sqlQuery)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("AssetTagApi");
                var request = new { SqlQuery = sqlQuery };
                var response = await httpClient.PostAsJsonAsync("api/reports/ai/execute-sql", request);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (result.TryGetProperty("results", out var resultsElement))
                    {
                        return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(resultsElement.GetRawText())
                            ?? new List<Dictionary<string, object>>();
                    }
                }

                return new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing SQL");
                return new List<Dictionary<string, object>>();
            }
        }
    }
}