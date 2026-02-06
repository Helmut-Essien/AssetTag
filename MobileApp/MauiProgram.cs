using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Syncfusion.Maui.Toolkit.Hosting;
using MobileData.Data;
using MobileApp.ViewModels;

namespace MobileApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureSyncfusionToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .UseMauiCommunityToolkit();

            // ────────────────────────────────────────────────────────────────
            // Register SQLite + EF Core DbContext with real app data path
            // ────────────────────────────────────────────────────────────────
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "AssetTagOffline.db3");

            builder.Services.AddDbContext<LocalDbContext>((serviceProvider, options) =>
            {
                options.UseSqlite(
                    $"Data Source={dbPath};" +
                    "Cache=Shared;" +                  // Improves concurrency
                    "PRAGMA journal_mode=WAL;" +       // Write-Ahead Logging – better perf on mobile
                    "PRAGMA synchronous=NORMAL;"       // Balance between speed & safety
                );

#if DEBUG
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
#endif
            });

            // Register the dbPath as a singleton so LocalDbContext can resolve it
            builder.Services.AddSingleton(dbPath);

            // ────────────────────────────────────────────────────────────────
            // Register hosted service to apply migrations at startup
            // ────────────────────────────────────────────────────────────────
            builder.Services.AddHostedService<MigrationHostedService>();

            // ────────────────────────────────────────────────────────────────
            // Register ViewModels for dependency injection
            // ────────────────────────────────────────────────────────────────
            builder.Services.AddTransient<MainPageViewModel>();

            // ────────────────────────────────────────────────────────────────
            // Register Pages for dependency injection
            // ────────────────────────────────────────────────────────────────
            builder.Services.AddTransient<MainPage>();

            // ────────────────────────────────────────────────────────────────
            // Logging – keep your debug logging and add file/app logging if desired
            // ────────────────────────────────────────────────────────────────
#if DEBUG
            builder.Logging.AddDebug();
#endif
            // Optional: Add file logging for production crash reports
            // builder.Logging.AddFileLogger(options => { ... });

            return builder.Build();
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Production-ready migration application service
    // Runs once at app startup, logs success/failure
    // ────────────────────────────────────────────────────────────────────────
    public class MigrationHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MigrationHostedService> _logger;

        public MigrationHostedService(
            IServiceProvider serviceProvider,
            ILogger<MigrationHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

                _logger.LogInformation("Starting database migration... Path: {DbPath}",
                    Path.Combine(FileSystem.AppDataDirectory, "AssetTagOffline.db3"));

                await dbContext.Database.MigrateAsync(cancellationToken);

                _logger.LogInformation("Database migrations applied successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply database migrations.");

                // In production, you might want to:
                // 1. Show user-friendly message (e.g. via dialog)
                // 2. Fall back to read-only mode
                // 3. Report to telemetry (AppCenter, Sentry, etc.)

                // For now we just log – app can continue with potentially outdated schema
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}