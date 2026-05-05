using MobileApp.Services;
using Microsoft.Extensions.Logging;

namespace MobileApp.Services
{
    /// <summary>
    /// Performance-optimized background service for periodic sync operations
    /// - Runs every 15 minutes when app is active
    /// - Checks network connectivity before syncing
    /// - Checks battery level to avoid draining battery
    /// - Only syncs if there are pending changes
    /// - Skips sync if already in progress
    /// </summary>
    public class BackgroundSyncService : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundSyncService> _logger;
        private PeriodicTimer? _timer;
        private Task? _timerTask;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly SemaphoreSlim _syncGuard = new(1, 1);
        private DateTime _lastSyncAttempt = DateTime.MinValue;

        // Performance settings
        private const int SYNC_INTERVAL_MINUTES = 15;
        private const double MIN_BATTERY_LEVEL = 0.15; // 15% battery minimum
        private const int MIN_SECONDS_BETWEEN_SYNCS = 30; // Prevent rapid sync attempts

        public BackgroundSyncService(
            IServiceProvider serviceProvider,
            ILogger<BackgroundSyncService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Start the background sync timer
        /// </summary>
        public void Start()
        {
            if (_timer != null)
            {
                _logger.LogWarning("Background sync service already started");
                return;
            }

            _logger.LogInformation("Starting background sync service ({Minutes} minute interval)", SYNC_INTERVAL_MINUTES);
            
            _cancellationTokenSource = new CancellationTokenSource();
            _timer = new PeriodicTimer(TimeSpan.FromMinutes(SYNC_INTERVAL_MINUTES));
            _timerTask = RunPeriodicSyncAsync(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// Stop the background sync timer
        /// </summary>
        public void Stop()
        {
            _logger.LogInformation("Stopping background sync service");
            
            _cancellationTokenSource?.Cancel();
            _timer?.Dispose();
            _timer = null;
        }

        /// <summary>
        /// Trigger an immediate sync (outside of the periodic schedule)
        /// Used when network connectivity is restored
        /// </summary>
        public async Task TriggerImmediateSyncAsync()
        {
            _logger.LogInformation("Triggering immediate background sync");
            await PerformSyncAsync();
        }

        private async Task RunPeriodicSyncAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (await _timer!.WaitForNextTickAsync(cancellationToken))
                {
                    await PerformSyncAsync();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Background sync service cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background sync service encountered an error");
            }
        }

        private async Task PerformSyncAsync()
        {
            if (!await _syncGuard.WaitAsync(0))
            {
                _logger.LogDebug("Sync already in progress, skipping");
                return;
            }

            try
            {
                var timeSinceLastSync = DateTime.UtcNow - _lastSyncAttempt;
                if (timeSinceLastSync.TotalSeconds < MIN_SECONDS_BETWEEN_SYNCS)
                {
                    _logger.LogDebug("Sync attempted too soon after last attempt, skipping");
                    return;
                }

                _lastSyncAttempt = DateTime.UtcNow;

                if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                {
                    _logger.LogDebug("No internet connection, skipping background sync");
                    return;
                }

                var batteryLevel = Battery.ChargeLevel;
                var batteryState = Battery.State;

                if (batteryLevel < MIN_BATTERY_LEVEL && batteryState != BatteryState.Charging)
                {
                    _logger.LogInformation("Battery level too low ({Level:P0}), skipping background sync", batteryLevel);
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

                var pendingCount = await syncService.GetPendingSyncCountAsync();

                if (pendingCount > 0)
                {
                    _logger.LogInformation("Background sync starting ({Count} pending operations to push)...", pendingCount);
                }
                else
                {
                    _logger.LogDebug("Background sync starting (pull only - no local changes to push)...");
                }

                var (success, message) = await syncService.EnqueueFullSyncAsync();

                if (success)
                {
                    _logger.LogInformation("Background sync completed successfully: {Message}", message);
                }
                else
                {
                    _logger.LogWarning("Background sync failed: {Message}", message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background sync encountered an error");
            }
            finally
            {
                _syncGuard.Release();
            }
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
            _timerTask?.Dispose();
            _syncGuard?.Dispose();
        }
    }
}