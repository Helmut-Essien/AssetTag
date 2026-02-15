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
            // Register background service to apply migrations (non-blocking)
            // ────────────────────────────────────────────────────────────────
            builder.Services.AddSingleton<MigrationBackgroundService>();

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
    // Non-blocking background migration service
    // Runs migrations in background without blocking app startup
    // ────────────────────────────────────────────────────────────────────────
    public class MigrationBackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MigrationBackgroundService> _logger;
        private Task? _migrationTask;

        public MigrationBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<MigrationBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            
            // Start migration in background immediately (non-blocking)
            _migrationTask = Task.Run(RunMigrationsAsync);
        }

        private async Task RunMigrationsAsync()
        {
            try
            {
                // Small delay to let app UI initialize first
                await Task.Delay(100);
                
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

                // Check if there are pending migrations before running
                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
                
                if (pendingMigrations.Any())
                {
                    _logger.LogInformation("Applying {Count} pending database migrations in background... Path: {DbPath}",
                        pendingMigrations.Count(),
                        Path.Combine(FileSystem.AppDataDirectory, "AssetTagOffline.db3"));

                    await dbContext.Database.MigrateAsync();

                    _logger.LogInformation("Database migrations applied successfully.");
                }
                else
                {
                    _logger.LogInformation("Database is up to date. No migrations needed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply database migrations in background.");

                // In production, you might want to:
                // 1. Show user-friendly message (e.g. via dialog)
                // 2. Fall back to read-only mode
                // 3. Report to telemetry (AppCenter, Sentry, etc.)

                // For now we just log – app can continue with potentially outdated schema
            }
        }

        // Optional: Method to wait for migrations to complete if needed
        public Task WaitForCompletionAsync() => _migrationTask ?? Task.CompletedTask;
    }
}