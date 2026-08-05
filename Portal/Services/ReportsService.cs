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

        public async Task<List<Dictionary<string, object>>> GetReportAsync(string reportType, int? year = null,
            DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("AssetTagApi");
                var url = $"api/reports/{reportType}";
                
                // Build query string for fixed-assets-schedule
                if (reportType == "fixed-assets-schedule")
                {
                    var queryParams = new List<string>();
                    if (startDate.HasValue)
                        queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
                    if (endDate.HasValue)
                        queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");
                    
                    if (queryParams.Any())
                        url += "?" + string.Join("&", queryParams);
                }
                
                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Report API response for {reportType}: {content.Substring(0, Math.Min(200, content.Length))}...");
                    
                    // Deserialize to JsonElement first to handle any JSON structure
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(content);
                    
                    // Convert to list of dictionaries
                    var results = new List<Dictionary<string, object>>();
                    
                    // Handle fixed-assets-schedule special format
                    if (reportType == "fixed-assets-schedule")
                    {
                        return ConvertFixedAssetsScheduleToTable(jsonElement);
                    }
                    
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

        private List<Dictionary<string, object>> ConvertFixedAssetsScheduleToTable(JsonElement jsonElement)
        {
            var results = new List<Dictionary<string, object>>();
            
            if (!jsonElement.TryGetProperty("categories", out var categoriesElement) ||
                !jsonElement.TryGetProperty("activeAssetRows", out var rowsElement))
            {
                return results;
            }

            // Parse categories
            var categories = new List<(string id, string displayName)>();
            foreach (var cat in categoriesElement.EnumerateArray())
            {
                var id = cat.GetProperty("categoryId").GetString() ?? "";
                var name = cat.GetProperty("categoryName").GetString() ?? "";
                var rate = cat.TryGetProperty("depreciationRate", out var rateEl) && rateEl.ValueKind != JsonValueKind.Null
                    ? rateEl.GetDecimal()
                    : (decimal?)null;
                var displayName = rate.HasValue ? $"{name} ({rate:0.##}%)" : name;
                categories.Add((id, displayName));
            }

            // Parse active assets schedule rows
            foreach (var row in rowsElement.EnumerateArray())
            {
                var dict = new Dictionary<string, object>();
                var rowLabel = row.GetProperty("rowLabel").GetString() ?? "";
                dict[""] = rowLabel; // First column is the row label
                
                if (row.TryGetProperty("categoryValues", out var valuesElement))
                {
                    foreach (var cat in categories)
                    {
                        if (valuesElement.TryGetProperty(cat.id, out var valueEl) &&
                            valueEl.ValueKind != JsonValueKind.Null)
                        {
                            dict[cat.displayName] = valueEl.GetDecimal();
                        }
                        else
                        {
                            dict[cat.displayName] = "-";
                        }
                    }
                }
                
                if (row.TryGetProperty("total", out var totalEl) && totalEl.ValueKind != JsonValueKind.Null)
                {
                    dict["Total"] = totalEl.GetDecimal();
                }
                else
                {
                    dict["Total"] = rowLabel == "" ? "" : "-";
                }
                
                results.Add(dict);
            }

            return results;
        }

        private object ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => TryParseDateTime(element, out var dt) ? dt : element.GetString() ?? string.Empty,
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

        private static bool TryParseDateTime(JsonElement element, out DateTime result)
        {
            var text = element.GetString();
            if (text != null && DateTime.TryParse(text, out result))
                return true;
            result = default;
            return false;
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
                else
                {
                    try
                    {
                        var errorObj = await response.Content.ReadFromJsonAsync<JsonElement>();
                        if (errorObj.TryGetProperty("details", out var detailsElement))
                        {
                            return "ERROR: " + detailsElement.GetString();
                        }
                        if (errorObj.TryGetProperty("error", out var errorElement))
                        {
                            return "ERROR: " + errorElement.GetString();
                        }
                    }
                    catch
                    {
                        return $"ERROR: API request failed with status code {response.StatusCode}";
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI query");
                return "ERROR: " + ex.Message;
            }
        }

        public async Task<List<Dictionary<string, object>>> ExecuteAiQueryAsync(string question)
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
                return new List<Dictionary<string, object>>();
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            string errorMessage = $"Query execution failed ({response.StatusCode})";
            try
            {
                var errorObj = JsonSerializer.Deserialize<JsonElement>(errorContent);
                if (errorObj.TryGetProperty("details", out var detailsElement))
                {
                    errorMessage = detailsElement.GetString() ?? errorMessage;
                }
                else if (errorObj.TryGetProperty("error", out var errorElement))
                {
                    errorMessage = errorElement.GetString() ?? errorMessage;
                }
            }
            catch {}

            throw new HttpRequestException(errorMessage);
        }

        public async Task<List<Dictionary<string, object>>> ExecuteSqlAsync(string sqlQuery)
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
                return new List<Dictionary<string, object>>();
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            string errorMessage = $"SQL execution failed ({response.StatusCode})";
            try
            {
                var errorObj = JsonSerializer.Deserialize<JsonElement>(errorContent);
                if (errorObj.TryGetProperty("details", out var detailsElement))
                {
                    errorMessage = detailsElement.GetString() ?? errorMessage;
                }
                else if (errorObj.TryGetProperty("error", out var errorElement))
                {
                    errorMessage = errorElement.GetString() ?? errorMessage;
                }
            }
            catch {}

            throw new HttpRequestException(errorMessage);
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