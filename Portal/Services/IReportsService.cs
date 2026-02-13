using System.Text.Json;

namespace Portal.Services
{
    public interface IReportsService
    {
        Task<List<Dictionary<string, object>>> GetReportAsync(string reportType, int? year = null);
        Task<JsonElement> GetDepreciationByDateRangeAsync(DateTime? startDate = null, DateTime? endDate = null,
            string? categoryId = null, string? departmentId = null, string? status = null);
        Task<string> GenerateAiQueryAsync(string question);
        Task<List<Dictionary<string, object>>> ExecuteAiQueryAsync(string question);
        Task<List<Dictionary<string, object>>> ExecuteSqlAsync(string sqlQuery);
        Task<bool> TestAiConnectionAsync();
    }
}