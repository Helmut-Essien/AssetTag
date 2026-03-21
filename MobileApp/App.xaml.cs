using MobileApp.Services;
using Shared.DTOs;

namespace MobileApp
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly BackgroundSyncService _backgroundSyncService;
        private readonly MigrationBackgroundService _migrationService;
        private readonly IVersionCheckService _versionCheckService;

        // Constructor injection for App
        public App(
            IServiceProvider serviceProvider,
            BackgroundSyncService backgroundSyncService,
            MigrationBackgroundService migrationService,
            IVersionCheckService versionCheckService)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            _backgroundSyncService = backgroundSyncService;
            _migrationService = migrationService;
            _versionCheckService = versionCheckService;

            // Migration service starts automatically in its constructor
            // No need to call Start() - it runs migrations in background

            // Start background sync service
            // Defer starting the background sync slightly so the UI can render the first frame
            // This avoids heavy work competing with initial UI rendering.
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Task.Delay(200); // give the UI a moment
                    _backgroundSyncService.Start();
                    
                    // Check for updates after a short delay (non-blocking)
                    await Task.Delay(2000); // Wait 2 seconds after app start
                    await CheckForUpdatesAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Background sync start failed: {ex.Message}");
                }
            });

            // Monitor network connectivity changes
            Connectivity.ConnectivityChanged += OnConnectivityChanged;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Resolve AppShell from DI container
            var shell = _serviceProvider.GetRequiredService<AppShell>();
            return new Window(shell);
        }

        /// <summary>
        /// Handle network connectivity changes
        /// Trigger immediate sync when internet is restored
        /// </summary>
        private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                // Internet restored - trigger immediate sync
                System.Diagnostics.Debug.WriteLine("Internet connection restored - triggering sync");
                
                // Use fire-and-forget to avoid blocking
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _backgroundSyncService.TriggerImmediateSyncAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Network restore sync failed: {ex.Message}");
                    }
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Internet connection lost");
            }
        }

        /// <summary>
        /// Check for app updates and show prompt if available
        /// </summary>
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                // Check if we've checked recently (within last 24 hours)
                var lastCheck = await _versionCheckService.GetLastCheckTimeAsync();
                if (lastCheck.HasValue && DateTime.UtcNow - lastCheck.Value < TimeSpan.FromHours(24))
                {
                    System.Diagnostics.Debug.WriteLine("Skipping update check - checked recently");
                    return;
                }

                var (updateAvailable, versionInfo, message) = await _versionCheckService.CheckForUpdateAsync();

                if (updateAvailable && versionInfo != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Update available: {versionInfo.LatestVersion}");
                    
                    // Show update prompt on main thread
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await ShowUpdatePromptAsync(versionInfo);
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"No update available: {message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Show update prompt to user using the new UpdateAvailablePage
        /// </summary>
        private async Task ShowUpdatePromptAsync(VersionCheckResponseDto versionInfo)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var updatePage = new Views.UpdateAvailablePage(_versionCheckService, versionInfo);
                    
                    // Show as modal page
                    await Application.Current?.Windows[0]?.Page?.Navigation.PushModalAsync(updatePage)!;
                    
                    // For mandatory updates, prevent dismissal by monitoring the modal stack
                    if (versionInfo.IsMandatory)
                    {
                        // Monitor if user tries to dismiss the modal
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(1000);
                            while (versionInfo.IsMandatory)
                            {
                                await Task.Delay(5000);
                                
                                // Check if modal was dismissed
                                var hasModal = await MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    var navigation = Application.Current?.Windows[0]?.Page?.Navigation;
                                    return navigation?.ModalStack.Count > 0;
                                });
                                
                                if (!hasModal)
                                {
                                    // Re-show the modal if it was dismissed
                                    await ShowUpdatePromptAsync(versionInfo);
                                    break;
                                }
                            }
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing update prompt: {ex.Message}");
            }
        }

        protected override void CleanUp()
        {
            // Unsubscribe from events
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;
            
            // Stop background sync service
            _backgroundSyncService?.Stop();
            
            base.CleanUp();
        }
    }
}