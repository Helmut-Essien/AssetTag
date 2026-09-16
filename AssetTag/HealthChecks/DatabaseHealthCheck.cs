using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssetTag.HealthChecks;

/// <summary>Readiness probe: verifies SQL Server is reachable (singleton-safe).</summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public DatabaseHealthCheck(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
        }

        // Match ApplicationDbContext: enable MARS for consistency with app connections.
        _connectionString = configured.Contains("MultipleActiveResultSets", StringComparison.OrdinalIgnoreCase)
            ? configured
            : configured + ";MultipleActiveResultSets=true";
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 5;
            _ = await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database is reachable.");
        }
        catch (Exception)
        {
            // Do not attach the exception — anonymous /health/ready must not leak SQL details.
            return HealthCheckResult.Unhealthy("Database unavailable.");
        }
    }
}
