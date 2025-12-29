using AssetTag.Data;
using Shared.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Data;
using System.Text.RegularExpressions;

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

    // Allowed tables - whitelist approach
    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Assets", "Categories", "Departments", "Locations", "AssetHistories", "AspNetUsers"
    };

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

            var schema = await GetDatabaseSchema();
            var prompt = $@"You are an expert SQL assistant for an Asset Management System. 
Generate a safe, read-only SQL SELECT query based on the natural language question.

DATABASE SCHEMA:
{JsonConvert.SerializeObject(schema, Newtonsoft.Json.Formatting.Indented)}

IMPORTANT RULES:
1. Generate ONLY SELECT queries - never DELETE, UPDATE, INSERT, DROP, TRUNCATE, ALTER, CREATE, GRANT, REVOKE, EXEC, or any dangerous operations
2. Use proper JOINs (INNER JOIN, LEFT JOIN, RIGHT JOIN) to connect related tables
3. Include WHERE clauses when filtering is needed
4. Use meaningful column aliases
5. Format the SQL cleanly with proper indentation
6. Only query from these tables: Assets, Categories, Departments, Locations, AssetHistories, AspNetUsers
7. For date filtering, use GETDATE() or CURRENT_TIMESTAMP
8. Use standard SQL functions like COUNT, SUM, AVG, MAX, MIN, GROUP BY, ORDER BY
11. You can use CTEs (WITH clause) for complex queries

OUTPUT FORMAT INSTRUCTIONS (FOLLOW EXACTLY):
- Output ONLY the SQL query
- Do not include any explanation, notes, or markdown
- Do not wrap in ```sql blocks
- Do not add any text before or after the query
- End the query with a semicolon only if required

QUESTION: {question}

Generate a safe SQL SELECT query:";

            var model = _configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";

            var requestData = new
            {
                model = model,
                messages = new[]
                {
                    /*new { role = "system", content = "You are a helpful SQL assistant that generates safe, read-only SQL queries for an Asset Management System." }*/
                    new {
                        role = "system",
                        content = "You are a SQL generator that generates safe, read-only SQL queries for an Asset Management System. Return ONLY the raw SQL code. " +
                      "Do not include any introductory text, explanations, or markdown code blocks. " +
                      "Just the SQL statement itself."
        },
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
            sql = CleanSqlResponse(sql);

            // Validate SQL safety with detailed feedback
            var validationResult = ValidateSqlSafety(sql);
            if (!validationResult.Item1)
            {
                _logger.LogWarning($"Generated SQL failed safety check: {validationResult.Item2}. SQL: {sql}");
                throw new InvalidOperationException($"Generated SQL contains potentially dangerous operations: {validationResult.Item2}");
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
            var validationResult = ValidateSqlSafety(sqlQuery);
            if (!validationResult.Item1)
            {
                throw new InvalidOperationException($"SQL query contains dangerous operations: {validationResult.Item2}");
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

    private string CleanSqlResponse(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;

        // Remove common markdown code fences
        sql = Regex.Replace(sql, @"^```(?:sql)?\s*\n?", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        sql = Regex.Replace(sql, @"\n?```$", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // Remove any leading text that is clearly explanatory
        // Match everything up to the first actual SQL keyword (SELECT or WITH)
        var match = Regex.Match(sql, @"(?i)\b(SELECT|WITH)\b", RegexOptions.Multiline);
        if (match.Success)
        {
            sql = sql.Substring(match.Index).Trim();
        }
        else
        {
            // If no SELECT or WITH found at all, something went wrong
            return string.Empty;
        }

        // Now remove everything AFTER the first complete SQL statement
        // Find the first semicolon that is not inside parentheses/quotes (simplified)
        // We'll take everything up to the last semicolon on or before the first major explanatory line
        var lines = sql.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
        var cleanedLines = new List<string>();
        bool inSql = true;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimStart();

            // Stop at obvious explanation markers
            if (inSql && (
                line.StartsWith("--", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("--", StringComparison.OrdinalIgnoreCase) == false || // not a comment
                line.StartsWith("/*", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Explanation:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Note:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("This query", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("The above", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("*", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("-", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(line, @"^\w+:$", RegexOptions.IgnoreCase) // labels like "Output:"
            ))
            {
                inSql = false;
            }

            if (inSql)
            {
                cleanedLines.Add(rawLine); // preserve original indentation
            }
            else if (!inSql && string.IsNullOrWhiteSpace(line))
            {
                // Allow blank lines, but stop adding content
                continue;
            }
            else if (!inSql)
            {
                break; // stop completely once explanation starts
            }
        }

        sql = string.Join("\n", cleanedLines).Trim();

        // Final cleanup: remove trailing semicolon if present (optional in SQL Server)
        if (sql.EndsWith(";"))
            sql = sql.Substring(0, sql.Length - 1).Trim();

        return sql;
    }

    private (bool isSafe, string reason) ValidateSqlSafety(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return (false, "SQL query is empty");
        }

        var normalizedSql = NormalizeSql(sql);

        // 1. Check for data modification commands (DML that modifies data)
        var modificationKeywords = new[]
        {
            @"\bDELETE\b", @"\bUPDATE\b", @"\bINSERT\b", @"\bMERGE\b"
        };

        foreach (var keyword in modificationKeywords)
        {
            if (Regex.IsMatch(normalizedSql, keyword, RegexOptions.IgnoreCase))
            {
                return (false, $"Contains prohibited data modification keyword: {keyword.Replace(@"\b", "").Replace(@"\\", "")}");
            }
        }

        // 2. Check for DDL (Data Definition Language) commands
        var ddlKeywords = new[]
        {
            @"\bDROP\b", @"\bTRUNCATE\b", @"\bALTER\b", @"\bCREATE\b"
        };

        foreach (var keyword in ddlKeywords)
        {
            if (Regex.IsMatch(normalizedSql, keyword, RegexOptions.IgnoreCase))
            {
                return (false, $"Contains prohibited DDL keyword: {keyword.Replace(@"\b", "").Replace(@"\\", "")}");
            }
        }

        // 3. Check for DCL (Data Control Language) commands
        var dclKeywords = new[]
        {
            @"\bGRANT\b", @"\bREVOKE\b", @"\bDENY\b"
        };

        foreach (var keyword in dclKeywords)
        {
            if (Regex.IsMatch(normalizedSql, keyword, RegexOptions.IgnoreCase))
            {
                return (false, $"Contains prohibited DCL keyword: {keyword.Replace(@"\b", "").Replace(@"\\", "")}");
            }
        }

        // 4. Check for dangerous system procedures and commands
        var systemCommands = new[]
        {
            @"\bEXEC\b", @"\bEXECUTE\b", @"\bSP_\w+", @"\bXP_\w+",
            @"\bSHUTDOWN\b", @"\bBACKUP\b", @"\bRESTORE\b", @"\bKILL\b"
        };

        foreach (var command in systemCommands)
        {
            if (Regex.IsMatch(normalizedSql, command, RegexOptions.IgnoreCase))
            {
                return (false, $"Contains prohibited system command: {command.Replace(@"\b", "").Replace(@"\\", "")}");
            }
        }

        // 5. Check for SQL injection patterns
        var injectionPatterns = new[]
        {
            @";--",           // SQL comment for injection
            @"--\s*$",        // Comment at end
            @"\bXP_CMDSHELL\b",
            @"\bSP_OACREATE\b",
            @"\bSP_OAMETHOD\b",
            @"@@\w+",         // System variables (excluding safe ones)
        };

        foreach (var pattern in injectionPatterns)
        {
            if (Regex.IsMatch(normalizedSql, pattern, RegexOptions.IgnoreCase))
            {
                return (false, $"Contains potential SQL injection pattern: {pattern}");
            }
        }

        // 6. Check that query starts with SELECT or WITH (for CTEs)
        var trimmedSql = normalizedSql.TrimStart();
        if (!trimmedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !trimmedSql.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Query must start with SELECT or WITH");
        }

        // 7. Validate table names (whitelist approach)
        if (!ValidateTableNames(normalizedSql))
        {
            return (false, "Query references unauthorized tables");
        }

        // 8. Check for stacked queries (multiple statements)
        // First, remove any trailing semicolons as they're optional in SQL Server
        var sqlForStatementCheck = normalizedSql.TrimEnd(';', ' ', '\t').Trim();

        var statementCount = sqlForStatementCheck.Split(';')
            .Select(s => s.Trim())
            .Count(s => !string.IsNullOrWhiteSpace(s));

        if (statementCount > 1)
        {
            return (false, "Multiple SQL statements are not allowed");
        }

        return (true, "Query is safe");
    }

    private string NormalizeSql(string sql)
    {
        // Remove multi-line comments
        sql = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);

        // Remove single-line comments (but preserve -- in strings)
        sql = Regex.Replace(sql, @"--[^\n\r]*", " ");

        // Normalize whitespace
        sql = Regex.Replace(sql, @"\s+", " ");

        return sql.Trim();
    }

    private bool ValidateTableNames(string sql)
    {
        // Extract table names from FROM and JOIN clauses
        var fromPattern = @"\bFROM\s+(\[?\w+\]?)(?:\s+(?:AS\s+)?(\w+))?";
        var joinPattern = @"\b(?:INNER\s+|LEFT\s+|RIGHT\s+|FULL\s+|CROSS\s+)?JOIN\s+(\[?\w+\]?)(?:\s+(?:AS\s+)?(\w+))?";

        var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Find FROM tables
        var fromMatches = Regex.Matches(sql, fromPattern, RegexOptions.IgnoreCase);
        foreach (Match match in fromMatches)
        {
            var tableName = match.Groups[1].Value.Trim('[', ']');
            tableNames.Add(tableName);
        }

        // Find JOIN tables
        var joinMatches = Regex.Matches(sql, joinPattern, RegexOptions.IgnoreCase);
        foreach (Match match in joinMatches)
        {
            var tableName = match.Groups[1].Value.Trim('[', ']');
            tableNames.Add(tableName);
        }

        // Check if all tables are in the whitelist
        foreach (var tableName in tableNames)
        {
            if (!AllowedTables.Contains(tableName))
            {
                _logger.LogWarning($"Unauthorized table referenced: {tableName}");
                return false;
            }
        }

        return true;
    }

    private async Task<DatabaseSchema> GetDatabaseSchema()
    {
        try
        {
            var schema = new DatabaseSchema
            {
                Tables = new List<TableSchema>()
            };

            foreach (var tableName in AllowedTables)
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
            return @"SELECT TOP 50 AssetTag, Name, CurrentValue,
                    DepreciationRate,
                    (CurrentValue * DepreciationRate / 100) as YearlyDepreciation,
                    (CurrentValue * DepreciationRate / 12 / 100) as MonthlyDepreciation
                    FROM Assets
                    WHERE CurrentValue IS NOT NULL
                    ORDER BY CurrentValue DESC";
        }
        else
        {
            return @"SELECT TOP 50 a.AssetTag, a.Name, a.Status, a.Condition, 
                    ISNULL(a.CurrentValue, 0) as CurrentValue,
                    c.Name as Category,
                    d.Name as Department
                    FROM Assets a
                    LEFT JOIN Categories c ON a.CategoryId = c.CategoryId
                    LEFT JOIN Departments d ON a.DepartmentId = d.DepartmentId
                    ORDER BY a.Name";
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