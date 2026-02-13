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
                    _logger.LogInformation($"Report API response for {reportType}: {content.Substring(0, Math.Min(200, content.Length))}...");
                    
                    // Deserialize to JsonElement first to handle any JSON structure
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(content);
                    
                    // Convert to list of dictionaries
                    var results = new List<Dictionary<string, object>>();
                    
                    if (jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in jsonElement.EnumerateArray())
                        {
                            var dict = new Dictionary<string, object>();
                            foreach (var property in item.EnumerateObject())
                            {
                                dict[property.Name] = ConvertJsonElement(property.Value);
                            }
                            results.Add(dict);
                        }
                    }
                    
                    return results;
                }

                _logger.LogError($"Failed to get report: {response.StatusCode}");
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Error content: {errorContent}");
                return new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting report {reportType}");
                return new List<Dictionary<string, object>>();
            }
        }

        private object ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal :
                                       element.TryGetInt64(out var longVal) ? longVal :
                                       element.TryGetDecimal(out var decVal) ? decVal :
                                       element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
                JsonValueKind.Object => element.EnumerateObject()
                    .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
                _ => element.ToString()
            };
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

        public async Task<bool> TestAiConnectionAsync()
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("AssetTagApi");
                var response = await httpClient.GetAsync("api/reports/ai/test-connection");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (result.TryGetProperty("connected", out var connectedElement))
                    {
                        return connectedElement.GetBoolean();
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing AI connection");
                return false;
            }
        }
    }
}