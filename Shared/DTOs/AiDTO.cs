using System.Text.Json.Serialization;

namespace Shared.DTOs
{
    public class AiQueryRequestDTO
    {
        [JsonPropertyName("question")]
        public string Question { get; set; } = string.Empty;

        [JsonPropertyName("generateSqlOnly")]
        public bool GenerateSqlOnly { get; set; } = false;
    }

    public class AiQueryResponseDTO
    {
        [JsonPropertyName("sqlQuery")]
        public string SqlQuery { get; set; } = string.Empty;

        [JsonPropertyName("results")]
        public List<Dictionary<string, object>> Results { get; set; } = new();

        [JsonPropertyName("naturalLanguageQuery")]
        public string NaturalLanguageQuery { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("executionTimeMs")]
        public double ExecutionTimeMs { get; set; }

        [JsonPropertyName("rowCount")]
        public int RowCount { get; set; }
    }

    public class ExecuteSqlRequestDTO
    {
        [JsonPropertyName("sqlQuery")]
        public string SqlQuery { get; set; } = string.Empty;
    }

    public class ExecuteSqlResponseDTO
    {
        [JsonPropertyName("results")]
        public List<Dictionary<string, object>> Results { get; set; } = new();

        [JsonPropertyName("rowCount")]
        public int RowCount { get; set; }

        [JsonPropertyName("sqlQuery")]
        public string SqlQuery { get; set; } = string.Empty;

        [JsonPropertyName("executedAt")]
        public DateTime ExecutedAt { get; set; }
    }

    public class ReportResponseDTO
    {
        [JsonPropertyName("reportType")]
        public string ReportType { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public List<Dictionary<string, object>> Data { get; set; } = new();

        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [JsonPropertyName("rowCount")]
        public int RowCount { get; set; }
    }

    public class GroqSettingsDTO
    {
        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("model")]
        public string Model { get; set; } = "mixtral-8x7b-32768";

        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/chat/completions";

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.1;

        [JsonPropertyName("maxTokens")]
        public int MaxTokens { get; set; } = 1000;
    }

    public class ChatMessageDTO
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user"; // "system", "user", "assistant"

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ChatHistoryDTO
    {
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ChatMessageDTO> Messages { get; set; } = new();

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}