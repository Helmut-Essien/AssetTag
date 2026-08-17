using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MobileData.Data;

namespace MobileApp.Tests.Helpers;

/// <summary>
/// In-memory SQLite database with a shared open connection so scoped
/// LocalDbContext instances see the same schema and data.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly ServiceProvider _provider;

    public SqliteConnection Connection { get; }

    public IServiceProvider Services => _provider;

    public SqliteTestDatabase()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        Connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton("memory");
        services.AddDbContext<LocalDbContext>(
            options => options.UseSqlite(Connection),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
        db.Database.EnsureCreated();
    }

    public LocalDbContext CreateContext()
    {
        var options = _provider.GetRequiredService<DbContextOptions<LocalDbContext>>();
        return new LocalDbContext(options, "memory");
    }

    public void Dispose()
    {
        _provider.Dispose();
        Connection.Dispose();
    }
}
