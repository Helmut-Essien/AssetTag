using AssetTag.Data;
using AssetTag.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Data;
using System.Xml;

namespace AssetTag.Services;

public interface IAIQueryService
{
    Task<string> GenerateSqlFromNaturalLanguage(string question);
    Task<List<Dictionary<string, object>>> ExecuteSafeQuery(string sqlQuery);
    Task<object> ProcessNaturalLanguageQuery(string question);
    Task<bool> TestGroqConnection();
}

public class AIQueryService : IAIQueryService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AIQueryService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public AIQueryService(
        ApplicationDbContext context,
        ILogger<AIQueryService> logger,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;

        // Configure HttpClient for Groq API
        var apiKey = _configuration["Groq:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<string> GenerateSqlFromNaturalLanguage(string question)
    {
        try
        {
            _logger.LogInformation($"Generating SQL for question: {question}");

            // Get database schema for context
            var schema = await GetDatabaseSchema();

            // Prepare prompt for LLM
            var prompt = $@"You are an expert SQL assistant for an Asset Management System. 
Generate a safe, read-only SQL SELECT query based on the natural language question.

DATABASE SCHEMA:
{JsonConvert.SerializeObject(schema, Newtonsoft.Json.Formatting.Indented)}

IMPORTANT RULES:
1. Generate ONLY SELECT queries - never DELETE, UPDATE, INSERT, DROP, TRUNCATE, ALTER, CREATE, GRANT, REVOKE, EXEC, or any dangerous operations
2. Use proper JOINs to connect related tables
3. Include WHERE clauses when filtering is needed
4. Use meaningful column aliases
5. Format the SQL cleanly with proper indentation
6. Always limit results to reasonable amounts (use TOP, LIMIT, or FETCH FIRST)
7. Only query from these tables: Assets, Categories, Departments, Locations, AssetHistories, AspNetUsers
8. For date filtering, use GETDATE() or CURRENT_TIMESTAMP

QUESTION: {question}

Generate a safe SQL SELECT query:";

            var model = _configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";

            var requestData = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful SQL assistant that generates safe, read-only SQL queries for an Asset Management System." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.1,
                max_tokens = 1000,
                top_p = 0.9
            };

            var jsonContent = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                "https://api.groq.com/openai/v1/chat/completions",
                content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Groq API error: {response.StatusCode} - {errorContent}");
                return GenerateFallbackQuery(question);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject(responseContent);

            var sql = result.choices[0].message.content.ToString();

            // Clean up the SQL
            sql = sql.Trim()
                     .Replace("```sql", "")
                     .Replace("```", "")
                     .Trim();

            // Remove any explanatory text before or after SQL
            var sqlStart = sql.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
            if (sqlStart > 0)
            {
                sql = sql.Substring(sqlStart);
            }

            // Validate SQL safety
            if (!IsSqlSafe(sql))
            {
                _logger.LogWarning($"Generated SQL failed safety check: {sql}");
                throw new InvalidOperationException("Generated SQL contains potentially dangerous operations");
            }

            _logger.LogInformation($"Successfully generated SQL: {sql.Substring(0, Math.Min(100, sql.Length))}...");
            return sql;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating SQL from natural language for question: {question}");
            return GenerateFallbackQuery(question);
        }
    }

    public async Task<List<Dictionary<string, object>>> ExecuteSafeQuery(string sqlQuery)
    {
        try
        {
            // Double-check SQL safety
            if (!IsSqlSafe(sqlQuery))
            {
                throw new InvalidOperationException("SQL query contains dangerous operations");
            }

            _logger.LogInformation($"Executing safe SQL query: {sqlQuery.Substring(0, Math.Min(100, sqlQuery.Length))}...");

            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sqlQuery;

            if (command.Connection.State != ConnectionState.Open)
            {
                await command.Connection.OpenAsync();
            }

            using var reader = await command.ExecuteReaderAsync();

            var results = new List<Dictionary<string, object>>();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[reader.GetName(i)] = value == DBNull.Value ? null : value;
                }
                results.Add(row);
            }

            _logger.LogInformation($"Query executed successfully, returned {results.Count} rows");
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing SQL query: {sqlQuery}");
            throw;
        }
    }

    public async Task<object> ProcessNaturalLanguageQuery(string question)
    {
        try
        {
            var startTime = DateTime.UtcNow;

            var sqlQuery = await GenerateSqlFromNaturalLanguage(question);
            var results = await ExecuteSafeQuery(sqlQuery);

            var executionTime = DateTime.UtcNow - startTime;

            return new
            {
                sqlQuery,
                results,
                timestamp = DateTime.UtcNow,
                executionTimeMs = executionTime.TotalMilliseconds,
                rowCount = results.Count,
                question = question
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing natural language query");
            throw;
        }
    }

    public async Task<bool> TestGroqConnection()
    {
        try
        {
            var requestData = new
            {
                model = _configuration["Groq:Model"] ?? "mixtral-8x7b-32768",
                messages = new[]
                {
                    new { role = "user", content = "Say 'Hello' if you are working." }
                },
                max_tokens = 10
            };

            var jsonContent = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                "https://api.groq.com/openai/v1/chat/completions",
                content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Groq connection");
            return false;
        }
    }

    private async Task<DatabaseSchema> GetDatabaseSchema()
    {
        try
        {
            // Get table names
            var tableNames = new List<string> { "Assets", "Categories", "Departments", "Locations", "AssetHistories", "AspNetUsers" };

            var schema = new DatabaseSchema
            {
                Tables = new List<TableSchema>()
            };

            foreach (var tableName in tableNames)
            {
                var columns = await GetTableColumns(tableName);
                schema.Tables.Add(new TableSchema
                {
                    Name = tableName,
                    Description = GetTableDescription(tableName),
                    Columns = columns
                });
            }

            return schema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database schema");
            return GetDefaultSchema();
        }
    }

    private async Task<List<ColumnSchema>> GetTableColumns(string tableName)
    {
        var columns = new List<ColumnSchema>();

        try
        {
            // This is a simplified version. In production, you might want to query INFORMATION_SCHEMA
            switch (tableName.ToLower())
            {
                case "assets":
                    columns = new List<ColumnSchema>
                    {
                        new() { Name = "AssetId", Type = "int", Description = "Primary key, unique identifier" },
                        new() { Name = "AssetTag", Type = "string", Description = "Unique asset tag/identifier" },
                        new() { Name = "Name", Type = "string", Description = "Asset name/description" },
                        new() { Name = "Status", Type = "string", Description = "Available, In Use, Under Maintenance, Retired, Lost" },
                        new() { Name = "Condition", Type = "string", Description = "Excellent, Good, Fair, Poor, Broken" },
                        new() { Name = "CurrentValue", Type = "decimal", Description = "Current monetary value" },
                        new() { Name = "DepartmentId", Type = "int", Description = "Foreign key to Departments" },
                        new() { Name = "CategoryId", Type = "int", Description = "Foreign key to Categories" },
                        new() { Name = "LocationId", Type = "int", Description = "Foreign key to Locations" },
                        new() { Name = "AssignedToUserId", Type = "string", Description = "Foreign key to AspNetUsers" },
                        new() { Name = "WarrantyExpiry", Type = "DateTime", Description = "Warranty expiration date" },
                        new() { Name = "DepreciationRate", Type = "decimal", Description = "Annual depreciation rate percentage" },
                        new() { Name = "PurchaseDate", Type = "DateTime", Description = "Date when asset was purchased" }
                    };
                    break;

                case "categories":
                    columns = new List<ColumnSchema>
                    {
                        new() { Name = "CategoryId", Type = "int", Description = "Primary key" },
                        new() { Name = "Name", Type = "string", Description = "Category name" },
                        new() { Name = "Description", Type = "string", Description = "Category description" }
                    };
                    break;

                case "departments":
                    columns = new List<ColumnSchema>
                    {
                        new() { Name = "DepartmentId", Type = "int", Description = "Primary key" },
                        new() { Name = "Name", Type = "string", Description = "Department name" },
                        new() { Name = "Description", Type = "string", Description = "Department description" }
                    };
                    break;

                case "locations":
                    columns = new List<ColumnSchema>
                    {
                        new() { Name = "LocationId", Type = "int", Description = "Primary key" },
                        new() { Name = "Name", Type = "string", Description = "Location name" },
                        new() { Name = "Campus", Type = "string", Description = "Campus name" }
                    };
                    break;

                case "assethistories":
                    columns = new List<ColumnSchema>
                    {
                        new() { Name = "HistoryId", Type = "int", Description = "Primary key" },
                        new() { Name = "AssetId", Type = "int", Description = "Foreign key to Assets" },
                        new() { Name = "Action", Type = "string", Description = "Action performed (Added, Updated, Moved, etc.)" },
                        new() { Name = "Description", Type = "string", Description = "Action description" },
                        new() { Name = "Timestamp", Type = "DateTime", Description = "When the action occurred" },
                        new() { Name = "UserId", Type = "string", Description = "Who performed the action" }
                    };
                    break;

                case "aspnetusers":
                    columns = new List<ColumnSchema>
                    {
                        new() { Name = "Id", Type = "string", Description = "Primary key" },
                        new() { Name = "FirstName", Type = "string", Description = "User first name" },
                        new() { Name = "Surname", Type = "string", Description = "User surname" },
                        new() { Name = "Email", Type = "string", Description = "User email" },
                        new() { Name = "DepartmentId", Type = "int", Description = "Foreign key to Departments" },
                        new() { Name = "IsActive", Type = "bit", Description = "Whether user is active" }
                    };
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting columns for table {tableName}");
        }

        return columns;
    }

    private string GetTableDescription(string tableName)
    {
        return tableName.ToLower() switch
        {
            "assets" => "Main assets table containing all asset information",
            "categories" => "Asset categories for classification",
            "departments" => "Organization departments",
            "locations" => "Physical locations where assets are kept",
            "assethistories" => "Audit trail of all asset changes and movements",
            "aspnetusers" => "Application users who can be assigned assets",
            _ => "Database table"
        };
    }

    private DatabaseSchema GetDefaultSchema()
    {
        return new DatabaseSchema
        {
            Tables = new List<TableSchema>
            {
                new TableSchema
                {
                    Name = "Assets",
                    Description = "Main assets table",
                    Columns = new List<ColumnSchema>
                    {
                        new() { Name = "AssetId", Type = "int", Description = "Primary key" },
                        new() { Name = "AssetTag", Type = "string", Description = "Unique asset identifier" },
                        new() { Name = "Name", Type = "string", Description = "Asset name" },
                        new() { Name = "Status", Type = "string", Description = "Asset status" },
                        new() { Name = "Condition", Type = "string", Description = "Asset condition" },
                        new() { Name = "CurrentValue", Type = "decimal", Description = "Current value" }
                    }
                }
            }
        };
    }

    private bool IsSqlSafe(string sql)
    {
        // Convert to lowercase for case-insensitive checking
        var lowerSql = sql.ToLowerInvariant();

        // List of dangerous keywords
        var dangerousKeywords = new[]
        {
            "delete", "update", "insert", "drop", "truncate", "alter",
            "create", "grant", "revoke", "exec", "execute", "sp_", "xp_",
            "shutdown", "backup", "restore", "kill", "union all", "--", ";--",
            "/*", "*/", "@@", "char(", "nchar(", "varchar(", "nvarchar(",
            "alter", "begin", "cast(", "convert(", "declare", "exec", "execute",
            "fetch", "kill", "open", "sys", "sysobjects", "syscolumns",
            "xp_cmdshell", "sp_oacreate", "sp_oamethod", "sp_oagetproperty"
        };

        // Check for any dangerous keywords
        foreach (var keyword in dangerousKeywords)
        {
            if (lowerSql.Contains(keyword))
            {
                _logger.LogWarning($"Potential dangerous SQL keyword detected: {keyword}");
                return false;
            }
        }

        // Check that it starts with SELECT (allow with/cte)
        var trimmedSql = lowerSql.TrimStart();
        if (!trimmedSql.StartsWith("select") && !trimmedSql.StartsWith("with"))
        {
            _logger.LogWarning("SQL does not start with SELECT or WITH");
            return false;
        }

        return true;
    }

    private string GenerateFallbackQuery(string question)
    {
        var lowerQuestion = question.ToLowerInvariant();

        if (lowerQuestion.Contains("department"))
        {
            if (lowerQuestion.Contains("count") || lowerQuestion.Contains("how many"))
            {
                return @"SELECT d.Name as Department, 
                        COUNT(a.AssetId) as AssetCount,
                        SUM(ISNULL(a.CurrentValue, 0)) as TotalValue
                        FROM Assets a
                        LEFT JOIN Departments d ON a.DepartmentId = d.DepartmentId
                        GROUP BY d.Name
                        ORDER BY AssetCount DESC";
            }
            return @"SELECT TOP 100 a.AssetTag, a.Name as AssetName, a.Status, a.Condition,
                    d.Name as Department, c.Name as Category, l.Name as Location
                    FROM Assets a
                    LEFT JOIN Departments d ON a.DepartmentId = d.DepartmentId
                    LEFT JOIN Categories c ON a.CategoryId = c.CategoryId
                    LEFT JOIN Locations l ON a.LocationId = l.LocationId
                    WHERE d.Name IS NOT NULL
                    ORDER BY d.Name, a.Name";
        }
        else if (lowerQuestion.Contains("status"))
        {
            return @"SELECT Status, 
                    COUNT(*) as Count,
                    AVG(ISNULL(CurrentValue, 0)) as AverageValue,
                    SUM(ISNULL(CurrentValue, 0)) as TotalValue
                    FROM Assets
                    GROUP BY Status
                    ORDER BY Count DESC";
        }
        else if (lowerQuestion.Contains("location") || lowerQuestion.Contains("where"))
        {
            return @"SELECT l.Name as Location, l.Campus,
                    COUNT(a.AssetId) as AssetCount,
                    SUM(ISNULL(a.CurrentValue, 0)) as TotalValue
                    FROM Assets a
                    LEFT JOIN Locations l ON a.LocationId = l.LocationId
                    GROUP BY l.Name, l.Campus
                    ORDER BY AssetCount DESC";
        }
        else if (lowerQuestion.Contains("warranty") || lowerQuestion.Contains("expir"))
        {
            return @"SELECT TOP 50 AssetTag, Name, WarrantyExpiry,
                    DATEDIFF(day, GETDATE(), WarrantyExpiry) as DaysUntilExpiry,
                    CurrentValue, Status
                    FROM Assets
                    WHERE WarrantyExpiry IS NOT NULL
                    AND WarrantyExpiry > GETDATE()
                    ORDER BY WarrantyExpiry";
        }
        else if (lowerQuestion.Contains("maintenance") || lowerQuestion.Contains("repair"))
        {
            return @"SELECT TOP 50 a.AssetTag, a.Name, a.Condition, a.Status,
                    c.Name as Category, d.Name as Department,
                    MAX(h.Timestamp) as LastMaintenanceDate
                    FROM Assets a
                    LEFT JOIN Categories c ON a.CategoryId = c.CategoryId
                    LEFT JOIN Departments d ON a.DepartmentId = d.DepartmentId
                    LEFT JOIN AssetHistories h ON a.AssetId = h.AssetId AND h.Action = 'Maintenance'
                    WHERE a.Condition IN ('Fair', 'Poor', 'Broken') OR a.Status = 'Under Maintenance'
                    GROUP BY a.AssetTag, a.Name, a.Condition, a.Status, c.Name, d.Name
                    ORDER BY a.Condition, LastMaintenanceDate";
        }
        else if (lowerQuestion.Contains("value") || lowerQuestion.Contains("worth"))
        {
            return @"SELECT AssetTag, Name, CurrentValue,
                    DepreciationRate,
                    (CurrentValue * DepreciationRate / 100) as YearlyDepreciation,
                    (CurrentValue * DepreciationRate / 12 / 100) as MonthlyDepreciation
                    FROM Assets
                    WHERE CurrentValue IS NOT NULL
                    ORDER BY CurrentValue DESC";
        }
        else
        {
            // Default query for general asset listing
            return @"SELECT TOP 50 AssetTag, Name, Status, Condition, 
                    ISNULL(CurrentValue, 0) as CurrentValue,
                    (SELECT Name FROM Categories WHERE CategoryId = a.CategoryId) as Category,
                    (SELECT Name FROM Departments WHERE DepartmentId = a.DepartmentId) as Department
                    FROM Assets a
                    ORDER BY Name";
        }
    }
}

// Schema classes
public class DatabaseSchema
{
    public List<TableSchema> Tables { get; set; } = new();
}

public class TableSchema
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ColumnSchema> Columns { get; set; } = new();
}

public class ColumnSchema
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}