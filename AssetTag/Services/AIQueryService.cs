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
            var schemaJson = JsonConvert.SerializeObject(schema, Newtonsoft.Json.Formatting.Indented);

            var prompt = $@"You are an expert T-SQL (Microsoft SQL Server) assistant for an Asset Management System.
Generate a safe, read-only SQL SELECT query based on the natural language question.

DATABASE SCHEMA AND RELATIONSHIPS:
{schemaJson}

CRITICAL RULES:
1. Generate ONLY SELECT queries - never DELETE, UPDATE, INSERT, DROP, TRUNCATE, ALTER, CREATE, GRANT, REVOKE, EXEC, or any dangerous operations
2. ALL ID columns (AssetId, CategoryId, DepartmentId, LocationId, HistoryId, UserId, Id) are STRINGS (ULID format like '01JQHZ...'), NOT integers. Always quote ID values in single quotes.
3. Always include TOP 1000 for unbounded queries to limit result size
4. Use proper JOINs based on the Relationships section provided for each table
5. Use meaningful column aliases with AS
6. Format the SQL cleanly with proper indentation
7. Use SQL Server functions: GETDATE() for current datetime, DATEDIFF() for date differences, ISNULL() or COALESCE() for null handling
8. Standard SQL functions: COUNT, SUM, AVG, MAX, MIN, GROUP BY, ORDER BY
9. CTEs (WITH clause) are allowed for complex queries
10. Use CAST or CONVERT when mixing types in calculations

IMPORTANT - COMPUTED PROPERTIES (NOT database columns):
- NetBookValue, AccumulatedDepreciation, GainLossOnDisposal, CalculatedUsefulLifeYears, TotalCost are NOT real database columns.
  They are computed in application code. DO NOT SELECT them.
- To calculate accumulated depreciation: PurchasePrice * (c.DepreciationRate / 100.0) * DATEDIFF(DAY, PurchaseDate, ISNULL(DisposalDate, GETDATE())) / 365.25
- To calculate net book value: PurchasePrice - accumulated_depreciation (capped at >= 0)
- To calculate total cost: CostPerUnit * Quantity (or use PurchasePrice)
- To find disposed assets: WHERE DisposalDate IS NOT NULL OR Status = 'Disposed'
- To find active assets: WHERE (Status != 'Disposed' OR Status IS NULL) AND DisposalDate IS NULL

FEW-SHOT EXAMPLES:
Q: ""How many assets are in each department?""
SQL: SELECT TOP 100 d.Name AS Department, COUNT(a.AssetId) AS AssetCount
FROM Assets a
LEFT JOIN Departments d ON a.DepartmentId = d.DepartmentId
GROUP BY d.Name
ORDER BY AssetCount DESC

Q: ""Show me all disposed assets with their disposal value""
SQL: SELECT TOP 100 a.AssetTag, a.Name, a.PurchasePrice, a.DisposalDate, a.DisposalValue,
(a.DisposalValue - a.PurchasePrice) AS GainLoss
FROM Assets a
WHERE a.DisposalDate IS NOT NULL
ORDER BY a.DisposalDate DESC

Q: ""What is the total value of assets by category?""
SQL: SELECT TOP 50 c.Name AS Category, COUNT(a.AssetId) AS AssetCount,
SUM(ISNULL(a.PurchasePrice, 0)) AS TotalValue
FROM Assets a
INNER JOIN Categories c ON a.CategoryId = c.CategoryId
WHERE (a.Status != 'Disposed' OR a.Status IS NULL) AND a.DisposalDate IS NULL
GROUP BY c.Name
ORDER BY TotalValue DESC

OUTPUT FORMAT INSTRUCTIONS (FOLLOW EXACTLY):
- Output ONLY the SQL query
- Do not include any explanation, notes, or markdown
- Do not wrap in ```sql blocks
- Do not add any text before or after the query

QUESTION: {question}

Generate a safe T-SQL SELECT query:";

            var model = _configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";

            var requestData = new
            {
                model = model,
                messages = new[]
                {
                    new {
                        role = "system",
                        content = "You are a T-SQL generator for Microsoft SQL Server. Generate safe, read-only SELECT queries for an Asset Management System. Return ONLY the raw SQL code. Do not include any introductory text, explanations, or markdown code blocks. Just the SQL statement itself. ALL ID columns are strings (ULID)."
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
            dynamic? result = JsonConvert.DeserializeObject(responseContent);

            var sql = result?.choices?[0]?.message?.content?.ToString() ?? "";

            sql = CleanSqlResponse(sql);

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
            var validationResult = ValidateSqlSafety(sqlQuery);
            if (!validationResult.Item1)
            {
                throw new InvalidOperationException($"SQL query contains dangerous operations: {validationResult.Item2}");
            }

            // Auto-inject TOP 1000 if no TOP clause present and no aggregate-only query
            sqlQuery = AutoInjectRowLimit(sqlQuery);

            _logger.LogInformation($"Executing safe SQL query: {sqlQuery.Substring(0, Math.Min(100, sqlQuery.Length))}...");

            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sqlQuery;
            command.CommandTimeout = 30; // 30-second timeout

            if (command.Connection?.State != ConnectionState.Open)
            {
                await command.Connection!.OpenAsync();
            }

            using var reader = await command.ExecuteReaderAsync();

            var results = new List<Dictionary<string, object>>();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[reader.GetName(i)] = value == DBNull.Value ? (object)"" : value;
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

    private string AutoInjectRowLimit(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql;

        var normalized = NormalizeSql(sql).TrimStart();

        // Only inject TOP if: starts with SELECT, doesn't already have TOP, and isn't purely aggregate
        if (!normalized.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return sql;

        if (Regex.IsMatch(normalized, @"\bSELECT\s+(DISTINCT\s+)?TOP\s+\d+", RegexOptions.IgnoreCase))
            return sql;

        // Check if this query is purely an aggregation (no detail rows)
        // If every selected expression is an aggregate function, TOP is unnecessary
        var selectMatch = Regex.Match(sql, @"(?i)SELECT\s+(.+?)\s+FROM", RegexOptions.Singleline);
        if (selectMatch.Success)
        {
            var columnsClause = selectMatch.Groups[1].Value;
            var nonAggColumns = Regex.Replace(columnsClause, @"\b(COUNT|SUM|AVG|MAX|MIN)\s*\([^)]*\)", "", RegexOptions.IgnoreCase);

            // If only aggregate functions or constants remain, skip TOP
            var remainingParts = nonAggColumns.Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p) && !Regex.IsMatch(p, @"^'.*'$")); // string literals

            if (!remainingParts.Any())
                return sql; // Pure aggregate query, no TOP needed
        }

        // Inject TOP 1000 after SELECT (and after DISTINCT if present)
        var match = Regex.Match(sql, @"(?i)\bSELECT\s+(DISTINCT\s+)?");
        if (match.Success)
        {
            var injectAt = match.Index + match.Length;
            sql = sql[..injectAt] + "TOP 1000 " + sql[injectAt..];
        }
        _logger.LogInformation("Auto-injected TOP 1000 into unbounded query");
        return sql;
    }

    private string CleanSqlResponse(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;

        // Remove common markdown code fences
        sql = Regex.Replace(sql, @"^```(?:sql)?\s*\n?", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        sql = Regex.Replace(sql, @"\n?```$", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // Remove any leading text that is clearly explanatory
        var match = Regex.Match(sql, @"(?i)\b(SELECT|WITH)\b", RegexOptions.Multiline);
        if (match.Success)
        {
            sql = sql.Substring(match.Index).Trim();
        }
        else
        {
            return string.Empty;
        }

        // Remove everything AFTER the first complete SQL statement
        var lines = sql.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
        var cleanedLines = new List<string>();
        var inSql = true;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimStart();

            if (inSql && (
                line.StartsWith("/*", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Explanation:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Note:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("This query", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("The above", StringComparison.OrdinalIgnoreCase) ||
                (line.StartsWith("*", StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(line, @"^\*\s*$")) ||
                line.StartsWith("-", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(line, @"^\w+:$", RegexOptions.IgnoreCase)
            ))
            {
                inSql = false;
            }

            if (inSql)
            {
                cleanedLines.Add(rawLine);
            }
            else if (!inSql && string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            else if (!inSql)
            {
                break;
            }
        }

        sql = string.Join("\n", cleanedLines).Trim();

        // Remove trailing semicolon
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

        // 1. Data modification commands
        var modificationKeywords = new[] { @"\bDELETE\b", @"\bUPDATE\b", @"\bINSERT\b", @"\bMERGE\b" };
        foreach (var keyword in modificationKeywords)
        {
            if (Regex.IsMatch(normalizedSql, keyword, RegexOptions.IgnoreCase))
                return (false, $"Contains prohibited data modification keyword: {keyword.Replace(@"\b", "").Replace(@"\\", "")}");
        }

        // 2. DDL commands
        var ddlKeywords = new[] { @"\bDROP\b", @"\bTRUNCATE\b", @"\bALTER\b", @"\bCREATE\b" };
        foreach (var keyword in ddlKeywords)
        {
            if (Regex.IsMatch(normalizedSql, keyword, RegexOptions.IgnoreCase))
                return (false, $"Contains prohibited DDL keyword: {keyword.Replace(@"\b", "").Replace(@"\\", "")}");
        }

        // 3. DCL commands
        var dclKeywords = new[] { @"\bGRANT\b", @"\bREVOKE\b", @"\bDENY\b" };
        foreach (var keyword in dclKeywords)
        {
            if (Regex.IsMatch(normalizedSql, keyword, RegexOptions.IgnoreCase))
                return (false, $"Contains prohibited DCL keyword: {keyword.Replace(@"\b", "").Replace(@"\\", "")}");
        }

        // 4. Dangerous system procedures
        var systemCommands = new[]
        {
            @"\bEXEC\b", @"\bEXECUTE\b", @"\bSP_\w+", @"\bXP_\w+",
            @"\bSHUTDOWN\b", @"\bBACKUP\b", @"\bRESTORE\b", @"\bKILL\b"
        };
        foreach (var command in systemCommands)
        {
            if (Regex.IsMatch(normalizedSql, command, RegexOptions.IgnoreCase))
                return (false, $"Contains prohibited system command: {command.Replace(@"\b", "").Replace(@"\\", "")}");
        }

        // 5. SQL injection patterns
        var injectionPatterns = new[]
        {
            @";--", @"\bXP_CMDSHELL\b", @"\bSP_OACREATE\b", @"\bSP_OAMETHOD\b"
        };
        foreach (var pattern in injectionPatterns)
        {
            if (Regex.IsMatch(normalizedSql, pattern, RegexOptions.IgnoreCase))
                return (false, $"Contains potential SQL injection pattern: {pattern}");
        }

        // 6. Must start with SELECT or WITH
        var trimmedSql = normalizedSql.TrimStart();
        if (!trimmedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !trimmedSql.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Query must start with SELECT or WITH");
        }

        // 7. Validate table names (whitelist)
        if (!ValidateTableNames(normalizedSql))
        {
            return (false, "Query references unauthorized tables");
        }

        // 8. No stacked queries (multiple statements)
        var sqlForStatementCheck = normalizedSql.TrimEnd(';', ' ', '\t').Trim();
        var statementCount = sqlForStatementCheck.Split(';')
            .Select(s => s.Trim())
            .Count(s => !string.IsNullOrWhiteSpace(s));

        if (statementCount > 1)
        {
            return (false, "Multiple SQL statements are not allowed");
        }

        // 9. Warn about SELECT * without TOP
        if (Regex.IsMatch(normalizedSql, @"\bSELECT\s+\*\s+FROM", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(normalizedSql, @"\bSELECT\s+(DISTINCT\s+)?TOP\s+", RegexOptions.IgnoreCase))
        {
            _logger.LogWarning("Query uses SELECT * without TOP — auto-injection will add TOP 1000");
        }

        return (true, "Query is safe");
    }

    private string NormalizeSql(string sql)
    {
        sql = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        sql = Regex.Replace(sql, @"--[^\n\r]*", " ");
        sql = Regex.Replace(sql, @"\s+", " ");
        return sql.Trim();
    }

    private bool ValidateTableNames(string sql)
    {
        var fromPattern = @"\bFROM\s+(\[?\w+\]?)(?:\s+(?:AS\s+)?(\w+))?";
        var joinPattern = @"\b(?:INNER\s+|LEFT\s+|RIGHT\s+|FULL\s+|CROSS\s+)?JOIN\s+(\[?\w+\]?)(?:\s+(?:AS\s+)?(\w+))?";

        var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var fromMatches = Regex.Matches(sql, fromPattern, RegexOptions.IgnoreCase);
        foreach (Match match in fromMatches)
        {
            var tableName = match.Groups[1].Value.Trim('[', ']');
            tableNames.Add(tableName);
        }

        var joinMatches = Regex.Matches(sql, joinPattern, RegexOptions.IgnoreCase);
        foreach (Match match in joinMatches)
        {
            var tableName = match.Groups[1].Value.Trim('[', ']');
            tableNames.Add(tableName);
        }

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
            var schema = new DatabaseSchema { Tables = new List<TableSchema>() };

            foreach (var tableName in AllowedTables)
            {
                var columns = await GetTableColumns(tableName);
                var relationships = GetTableRelationships(tableName);
                schema.Tables.Add(new TableSchema
                {
                    Name = tableName,
                    Description = GetTableDescription(tableName),
                    Columns = columns,
                    Relationships = relationships
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

    private Task<List<ColumnSchema>> GetTableColumns(string tableName)
    {
        var columns = new List<ColumnSchema>();

        try
        {
            switch (tableName.ToLower())
            {
                case "assets":
                    columns = new List<ColumnSchema>
                    {
                        new() { Name = "AssetId", Type = "nvarchar (ULID string)", Description = "Primary key, unique identifier" },
                        new() { Name = "AssetTag", Type = "nvarchar", Description = "Unique asset tag/barcode identifier" },
                        new() { Name = "OldAssetTag", Type = "nvarchar", Description = "Previous asset tag if changed" },
                        new() { Name = "DigitalAssetTag", Type = "nvarchar", Description = "Digital/RFID alternative tag" },
                        new() { Name = "Name", Type = "nvarchar(50)", Description = "Asset name/description" },
                        new() { Name = "Description", Type = "nvarchar", Description = "Detailed description of the asset" },
                        new() { Name = "CategoryId", Type = "nvarchar (ULID)", Description = "Foreign key to Categories (use JOIN)" },
                        new() { Name = "LocationId", Type = "nvarchar (ULID)", Description = "Foreign key to Locations (use JOIN)" },
                        new() { Name = "DepartmentId", Type = "nvarchar (ULID)", Description = "Foreign key to Departments (use JOIN)" },
                        new() { Name = "AssignedToUserId", Type = "nvarchar (ULID)", Description = "Foreign key to AspNetUsers (use JOIN)" },
                        new() { Name = "Status", Type = "nvarchar", Description = "Available, In Use, Under Maintenance, Disposed, Lost, Stolen" },
                        new() { Name = "Condition", Type = "nvarchar", Description = "Excellent, Good, Fair, Poor, Broken" },
                        new() { Name = "PurchaseDate", Type = "datetime2", Description = "Date when asset was purchased" },
                        new() { Name = "PurchasePrice", Type = "decimal", Description = "Original purchase/acquisition price" },
                        new() { Name = "CurrentValue", Type = "decimal", Description = "Current estimated monetary value (may be deprecated, prefer PurchasePrice)" },
                        new() { Name = "CostPerUnit", Type = "decimal", Description = "Cost per individual unit" },
                        new() { Name = "Quantity", Type = "int", Description = "Number of units (default 1)" },
                        new() { Name = "UsefulLifeYears", Type = "int", Description = "User-specified useful life in years (null = calculate from category depreciation rate)" },
                        new() { Name = "DepreciationRate", Type = "decimal", Description = "Annual depreciation rate percentage (may be overridden per asset)" },
                        new() { Name = "DisposalDate", Type = "datetime2", Description = "Date asset was disposed (null = not disposed)" },
                        new() { Name = "DisposalValue", Type = "decimal", Description = "Proceeds/amount received upon disposal" },
                        new() { Name = "SerialNumber", Type = "nvarchar", Description = "Manufacturer serial number" },
                        new() { Name = "VendorName", Type = "nvarchar", Description = "Vendor or supplier name" },
                        new() { Name = "InvoiceNumber", Type = "nvarchar", Description = "Purchase invoice reference number" },
                        new() { Name = "WarrantyExpiry", Type = "datetime2", Description = "Warranty expiration date" },
                        new() { Name = "Remarks", Type = "nvarchar", Description = "Additional notes or remarks" },
                        new() { Name = "CreatedAt", Type = "datetime2", Description = "Record creation timestamp (UTC)" },
                        new() { Name = "DateModified", Type = "datetime2", Description = "Last modification timestamp" },
                        new() { Name = "LastScannedAt", Type = "datetime2", Description = "Last time the asset tag was scanned" }
                    };
                    break;

                case "categories":
                    columns = new List<ColumnSchema>
                    {
                        new() { Name = "CategoryId", Type = "nvarchar (ULID)", Description = "Primary key (string ULID)" },
                        new() { Name = "Name", Type = "nvarchar", Description = "Category name (e.g. Furniture, Computers, Motor Vehicle)" },
                        new() { Name = "Description", Type = "nvarchar", Description = "Category description" },
                        new() { Name = "DepreciationRate", Type = "decimal", Description = "Annual depreciation rate as percentage (e.g. 20 = 20% per year)" },
                        new() { Name = "DateModified", Type = "datetime2", Description = "Last modification timestamp" }
                    };
                    break;

                case "departments":
                    columns = new List<ColumnSchema>
                    {
                        new() { Name = "DepartmentId", Type = "nvarchar (ULID)", Description = "Primary key (string ULID)" },
                        new() { Name = "Name", Type = "nvarchar", Description = "Department name" },
                        new() { Name = "Description", Type = "nvarchar", Description = "Department description" },
                        new() { Name = "DateModified", Type = "datetime2", Description = "Last modification timestamp" }
                    };
                    break;

                case "locations":
                    columns = new List<ColumnSchema>
                    {
                        new() { Name = "LocationId", Type = "nvarchar (ULID)", Description = "Primary key (string ULID)" },
                        new() { Name = "Name", Type = "nvarchar", Description = "Location name" },
                        new() { Name = "Description", Type = "nvarchar", Description = "Location description" },
                        new() { Name = "Campus", Type = "nvarchar", Description = "Campus name" },
                        new() { Name = "Building", Type = "nvarchar", Description = "Building name/number" },
                        new() { Name = "Room", Type = "nvarchar", Description = "Room number" },
                        new() { Name = "Latitude", Type = "float", Description = "GPS latitude coordinate" },
                        new() { Name = "Longitude", Type = "float", Description = "GPS longitude coordinate" },
                        new() { Name = "DateModified", Type = "datetime2", Description = "Last modification timestamp" }
                    };
                    break;

                case "assethistories":
                    columns = new List<ColumnSchema>
                    {
                        new() { Name = "HistoryId", Type = "nvarchar (ULID)", Description = "Primary key (string ULID)" },
                        new() { Name = "AssetId", Type = "nvarchar (ULID)", Description = "Foreign key to Assets (use JOIN)" },
                        new() { Name = "UserId", Type = "nvarchar (ULID)", Description = "Foreign key to AspNetUsers — who performed the action" },
                        new() { Name = "Action", Type = "nvarchar", Description = "Action type: Added, Updated, Maintenance, Transferred, Assigned, Unassigned, Disposed, StatusChanged" },
                        new() { Name = "Description", Type = "nvarchar", Description = "Human-readable action description" },
                        new() { Name = "Timestamp", Type = "datetime2", Description = "When the action occurred (UTC)" },
                        new() { Name = "OldLocationId", Type = "nvarchar (ULID)", Description = "Previous location (for transfers)" },
                        new() { Name = "NewLocationId", Type = "nvarchar (ULID)", Description = "New location (for transfers)" },
                        new() { Name = "OldStatus", Type = "nvarchar", Description = "Previous status (for status changes)" },
                        new() { Name = "NewStatus", Type = "nvarchar", Description = "New status (for status changes)" }
                    };
                    break;

                case "aspnetusers":
                    columns = new List<ColumnSchema>
                    {
                        new() { Name = "Id", Type = "nvarchar (ULID)", Description = "Primary key — user identifier (string ULID)" },
                        new() { Name = "FirstName", Type = "nvarchar", Description = "User first name" },
                        new() { Name = "Surname", Type = "nvarchar", Description = "User surname/last name" },
                        new() { Name = "OtherNames", Type = "nvarchar", Description = "Other/middle names" },
                        new() { Name = "Email", Type = "nvarchar", Description = "User email address" },
                        new() { Name = "UserName", Type = "nvarchar", Description = "Login username" },
                        new() { Name = "JobRole", Type = "nvarchar", Description = "Job title or role" },
                        new() { Name = "DepartmentId", Type = "nvarchar (ULID)", Description = "Foreign key to Departments" },
                        new() { Name = "Address", Type = "nvarchar", Description = "Physical address" },
                        new() { Name = "DateOfBirth", Type = "datetime2", Description = "Date of birth" },
                        new() { Name = "ProfileImage", Type = "nvarchar", Description = "Profile image URL/path" },
                        new() { Name = "IsActive", Type = "bit", Description = "Whether user account is active" },
                        new() { Name = "DateCreated", Type = "datetime2", Description = "Account creation date" }
                    };
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting columns for table {tableName}");
        }

        return Task.FromResult(columns);
    }

    private List<RelationshipInfo> GetTableRelationships(string tableName)
    {
        return tableName.ToLower() switch
        {
            "assets" => new List<RelationshipInfo>
            {
                new() { FromColumn = "CategoryId", ToTable = "Categories", ToColumn = "CategoryId", Description = "Asset belongs to this category" },
                new() { FromColumn = "DepartmentId", ToTable = "Departments", ToColumn = "DepartmentId", Description = "Asset assigned to this department" },
                new() { FromColumn = "LocationId", ToTable = "Locations", ToColumn = "LocationId", Description = "Asset physically located here" },
                new() { FromColumn = "AssignedToUserId", ToTable = "AspNetUsers", ToColumn = "Id", Description = "User assigned this asset" }
            },
            "assethistories" => new List<RelationshipInfo>
            {
                new() { FromColumn = "AssetId", ToTable = "Assets", ToColumn = "AssetId", Description = "History entry for this asset" },
                new() { FromColumn = "UserId", ToTable = "AspNetUsers", ToColumn = "Id", Description = "User who performed the action" },
                new() { FromColumn = "OldLocationId", ToTable = "Locations", ToColumn = "LocationId", Description = "Previous location (for transfers)" },
                new() { FromColumn = "NewLocationId", ToTable = "Locations", ToColumn = "LocationId", Description = "New location (for transfers)" }
            },
            "aspnetusers" => new List<RelationshipInfo>
            {
                new() { FromColumn = "DepartmentId", ToTable = "Departments", ToColumn = "DepartmentId", Description = "User belongs to this department" }
            },
            _ => new List<RelationshipInfo>()
        };
    }

    private string GetTableDescription(string tableName)
    {
        return tableName.ToLower() switch
        {
            "assets" => "Main assets table: contains all fixed asset records, financial data (purchase price, disposal info), assignments, and status tracking",
            "categories" => "Asset categories for classification with depreciation rates (e.g. Furniture at 10%, Computers at 25%)",
            "departments" => "Organization departments that own/use assets",
            "locations" => "Physical locations where assets are kept (with campus, building, room, GPS coordinates)",
            "assethistories" => "Audit trail — records every change to every asset (transfers, maintenance, status changes, disposals)",
            "aspnetusers" => "Application users who can be assigned assets or perform actions",
            _ => "Database table"
        };
    }

    private DatabaseSchema GetDefaultSchema()
    {
        return new DatabaseSchema
        {
            Tables = new List<TableSchema>
            {
                new()
                {
                    Name = "Assets",
                    Description = "Main assets table",
                    Columns = new List<ColumnSchema>
                    {
                        new() { Name = "AssetId", Type = "nvarchar (ULID)", Description = "Primary key" },
                        new() { Name = "AssetTag", Type = "nvarchar", Description = "Unique asset identifier" },
                        new() { Name = "Name", Type = "nvarchar", Description = "Asset name" },
                        new() { Name = "Status", Type = "nvarchar", Description = "Asset status" },
                        new() { Name = "Condition", Type = "nvarchar", Description = "Asset condition" },
                        new() { Name = "PurchasePrice", Type = "decimal", Description = "Purchase price" },
                        new() { Name = "DisposalDate", Type = "datetime2", Description = "Disposal date" },
                        new() { Name = "DisposalValue", Type = "decimal", Description = "Disposal proceeds" }
                    },
                    Relationships = new List<RelationshipInfo>
                    {
                        new() { FromColumn = "CategoryId", ToTable = "Categories", ToColumn = "CategoryId" },
                        new() { FromColumn = "DepartmentId", ToTable = "Departments", ToColumn = "DepartmentId" },
                        new() { FromColumn = "LocationId", ToTable = "Locations", ToColumn = "LocationId" }
                    }
                }
            }
        };
    }

    private string GenerateFallbackQuery(string question)
    {
        var lowerQuestion = question.ToLowerInvariant();

        // Disposals & gain/loss
        if (lowerQuestion.Contains("dispos") || lowerQuestion.Contains("gain") || lowerQuestion.Contains("loss on"))
        {
            return @"SELECT TOP 100 a.AssetTag, a.Name, c.Name AS Category, a.PurchasePrice,
                    a.DisposalDate, a.DisposalValue,
                    (a.DisposalValue - a.PurchasePrice) AS GainLossOnDisposal,
                    d.Name AS Department
                    FROM Assets a
                    LEFT JOIN Categories c ON a.CategoryId = c.CategoryId
                    LEFT JOIN Departments d ON a.DepartmentId = d.DepartmentId
                    WHERE a.DisposalDate IS NOT NULL
                    ORDER BY a.DisposalDate DESC";
        }

        // Depreciation
        if (lowerQuestion.Contains("depreciation") || lowerQuestion.Contains("depreciat"))
        {
            return @"SELECT TOP 100 a.AssetTag, a.Name, c.Name AS Category, a.PurchasePrice,
                    c.DepreciationRate,
                    a.PurchasePrice * ISNULL(c.DepreciationRate, 0) / 100.0 AS AnnualDepreciation,
                    a.PurchasePrice * ISNULL(c.DepreciationRate, 0) / 12.0 / 100.0 AS MonthlyDepreciation,
                    a.PurchaseDate,
                    DATEDIFF(YEAR, a.PurchaseDate, GETDATE()) AS AgeInYears
                    FROM Assets a
                    INNER JOIN Categories c ON a.CategoryId = c.CategoryId
                    WHERE a.PurchasePrice IS NOT NULL AND c.DepreciationRate IS NOT NULL
                      AND (a.DisposalDate IS NULL OR a.Status != 'Disposed')
                    ORDER BY AnnualDepreciation DESC";
        }

        // Department
        if (lowerQuestion.Contains("department"))
        {
            if (lowerQuestion.Contains("count") || lowerQuestion.Contains("how many"))
            {
                return @"SELECT d.Name AS Department, 
                        COUNT(a.AssetId) AS AssetCount,
                        SUM(ISNULL(a.PurchasePrice, 0)) AS TotalValue
                        FROM Assets a
                        LEFT JOIN Departments d ON a.DepartmentId = d.DepartmentId
                        GROUP BY d.Name
                        ORDER BY AssetCount DESC";
            }
            return @"SELECT TOP 100 a.AssetTag, a.Name AS AssetName, a.Status, a.Condition,
                    d.Name AS Department, c.Name AS Category, l.Name AS Location
                    FROM Assets a
                    LEFT JOIN Departments d ON a.DepartmentId = d.DepartmentId
                    LEFT JOIN Categories c ON a.CategoryId = c.CategoryId
                    LEFT JOIN Locations l ON a.LocationId = l.LocationId
                    WHERE d.Name IS NOT NULL
                    ORDER BY d.Name, a.Name";
        }

        // Status
        if (lowerQuestion.Contains("status"))
        {
            return @"SELECT Status, 
                    COUNT(*) AS Count,
                    SUM(ISNULL(PurchasePrice, 0)) AS TotalValue
                    FROM Assets
                    GROUP BY Status
                    ORDER BY Count DESC";
        }

        // Location
        if (lowerQuestion.Contains("location") || lowerQuestion.Contains("where"))
        {
            return @"SELECT l.Name AS Location, l.Campus, l.Building,
                    COUNT(a.AssetId) AS AssetCount,
                    SUM(ISNULL(a.PurchasePrice, 0)) AS TotalValue
                    FROM Assets a
                    LEFT JOIN Locations l ON a.LocationId = l.LocationId
                    GROUP BY l.Name, l.Campus, l.Building
                    ORDER BY AssetCount DESC";
        }

        // Warranty
        if (lowerQuestion.Contains("warranty") || lowerQuestion.Contains("expir"))
        {
            return @"SELECT TOP 50 AssetTag, Name, WarrantyExpiry,
                    DATEDIFF(DAY, GETDATE(), WarrantyExpiry) AS DaysUntilExpiry,
                    PurchasePrice, Status
                    FROM Assets
                    WHERE WarrantyExpiry IS NOT NULL
                    AND WarrantyExpiry > GETDATE()
                    ORDER BY WarrantyExpiry";
        }

        // Maintenance
        if (lowerQuestion.Contains("maintenance") || lowerQuestion.Contains("repair"))
        {
            return @"SELECT TOP 50 a.AssetTag, a.Name, a.Condition, a.Status,
                    c.Name AS Category, d.Name AS Department,
                    MAX(h.Timestamp) AS LastMaintenanceDate
                    FROM Assets a
                    LEFT JOIN Categories c ON a.CategoryId = c.CategoryId
                    LEFT JOIN Departments d ON a.DepartmentId = d.DepartmentId
                    LEFT JOIN AssetHistories h ON a.AssetId = h.AssetId AND h.Action = 'Maintenance'
                    WHERE a.Condition IN ('Fair', 'Poor', 'Broken') OR a.Status = 'Under Maintenance'
                    GROUP BY a.AssetTag, a.Name, a.Condition, a.Status, c.Name, d.Name
                    ORDER BY a.Condition, LastMaintenanceDate";
        }

        // Value / cost / price
        if (lowerQuestion.Contains("value") || lowerQuestion.Contains("cost") || lowerQuestion.Contains("price") || lowerQuestion.Contains("worth"))
        {
            return @"SELECT TOP 50 a.AssetTag, a.Name, a.PurchasePrice,
                    c.Name AS Category, c.DepreciationRate,
                    (a.PurchasePrice * ISNULL(c.DepreciationRate, 0) / 100.0) AS AnnualDepreciation,
                    a.PurchaseDate,
                    DATEDIFF(YEAR, a.PurchaseDate, GETDATE()) AS AgeInYears
                    FROM Assets a
                    INNER JOIN Categories c ON a.CategoryId = c.CategoryId
                    WHERE a.PurchasePrice IS NOT NULL
                    ORDER BY a.PurchasePrice DESC";
        }

        // Category
        if (lowerQuestion.Contains("categor"))
        {
            return @"SELECT c.Name AS Category, c.DepreciationRate,
                    COUNT(a.AssetId) AS AssetCount,
                    SUM(ISNULL(a.PurchasePrice, 0)) AS TotalValue
                    FROM Categories c
                    LEFT JOIN Assets a ON c.CategoryId = a.CategoryId
                    GROUP BY c.Name, c.DepreciationRate
                    ORDER BY AssetCount DESC";
        }

        // User / assigned / person
        if (lowerQuestion.Contains("user") || lowerQuestion.Contains("assign") || lowerQuestion.Contains("person") || lowerQuestion.Contains("staff"))
        {
            return @"SELECT TOP 100 CONCAT(u.FirstName, ' ', u.Surname) AS UserName, u.Email, u.JobRole,
                    COUNT(a.AssetId) AS AssignedAssets,
                    SUM(ISNULL(a.PurchasePrice, 0)) AS TotalValue
                    FROM AspNetUsers u
                    LEFT JOIN Assets a ON u.Id = a.AssignedToUserId
                    GROUP BY u.FirstName, u.Surname, u.Email, u.JobRole
                    ORDER BY AssignedAssets DESC";
        }

        // Default: comprehensive asset listing
        return @"SELECT TOP 100 a.AssetTag, a.Name, a.Status, a.Condition,
                a.PurchasePrice,
                c.Name AS Category, c.DepreciationRate,
                d.Name AS Department,
                l.Name AS Location, l.Campus,
                a.DisposalDate
                FROM Assets a
                LEFT JOIN Categories c ON a.CategoryId = c.CategoryId
                LEFT JOIN Departments d ON a.DepartmentId = d.DepartmentId
                LEFT JOIN Locations l ON a.LocationId = l.LocationId
                ORDER BY a.Name";
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
    public List<RelationshipInfo> Relationships { get; set; } = new();
}

public class ColumnSchema
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class RelationshipInfo
{
    public string FromColumn { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;
    public string ToColumn { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
