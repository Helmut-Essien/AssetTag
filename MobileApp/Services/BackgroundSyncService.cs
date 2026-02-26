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
        private bool _isSyncing = false;
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
            // Performance check: Prevent concurrent syncs
            if (_isSyncing)
            {
                _logger.LogDebug("Sync already in progress, skipping");
                return;
            }

            // Performance check: Rate limiting
            var timeSinceLastSync = DateTime.UtcNow - _lastSyncAttempt;
            if (timeSinceLastSync.TotalSeconds < MIN_SECONDS_BETWEEN_SYNCS)
            {
                _logger.LogDebug("Sync attempted too soon after last attempt, skipping");
                return;
            }

            _isSyncing = true;
            _lastSyncAttempt = DateTime.UtcNow;

            try
            {
                // Performance check: Network connectivity
                if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                {
                    _logger.LogDebug("No internet connection, skipping background sync");
                    return;
                }

                // Performance check: Battery level (only on mobile devices)
                var batteryLevel = Battery.ChargeLevel;
                var batteryState = Battery.State;
                
                // Skip sync if battery is low and not charging
                if (batteryLevel < MIN_BATTERY_LEVEL && batteryState != BatteryState.Charging)
                {
                    _logger.LogInformation("Battery level too low ({Level:P0}), skipping background sync", batteryLevel);
                    return;
                }

                // Performance check: Only sync if there are pending changes
                using var scope = _serviceProvider.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
                
                var pendingCount = await syncService.GetPendingSyncCountAsync();
                if (pendingCount == 0)
                {
                    _logger.LogDebug("No pending changes, skipping background sync");
                    return;
                }

                _logger.LogInformation("Background sync starting ({Count} pending operations)...", pendingCount);
                
                var (success, message) = await syncService.FullSyncAsync();
                
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
                _isSyncing = false;
            }
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
            _timerTask?.Dispose();
        }
    }
}