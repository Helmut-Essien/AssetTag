using System.Text.Json;

namespace Portal.Services
{
    public interface IReportsService
    {
        Task<List<Dictionary<string, object>>> GetReportAsync(string reportType);
        Task<string> GenerateAiQueryAsync(string question);
        Task<List<Dictionary<string, object>>> ExecuteAiQueryAsync(string question);
        Task<List<Dictionary<string, object>>> ExecuteSqlAsync(string sqlQuery);
        Task<bool> TestAiConnectionAsync();
    }
}