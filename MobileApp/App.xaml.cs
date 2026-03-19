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
        /// Show update prompt to user
        /// </summary>
        private async Task ShowUpdatePromptAsync(VersionCheckResponseDto versionInfo)
        {
            try
            {
                var currentVersion = _versionCheckService.GetCurrentVersion();
                var isMandatory = versionInfo.IsMandatory;

                var title = isMandatory ? "Critical Update Required" : "Update Available";
                var message = isMandatory
                    ? $"A critical update to version {versionInfo.LatestVersion} is required to continue using the app.\n\nCurrent version: {currentVersion}"
                    : $"Version {versionInfo.LatestVersion} is now available!\n\nCurrent version: {currentVersion}";

                // Add features if available
                if (versionInfo.Features.Length > 0)
                {
                    message += "\n\nWhat's new:\n" + string.Join("\n", versionInfo.Features.Select(f => $"• {f}"));
                }

                var updateButton = isMandatory ? "Update Now" : "Update";
                var cancelButton = isMandatory ? null : "Later";

                var mainPage = Application.Current?.Windows[0]?.Page;
                if (mainPage == null) return;

                var result = await mainPage.DisplayAlert(
                    title,
                    message,
                    updateButton,
                    cancelButton);

                if (result)
                {
                    // User chose to update
                    await DownloadAndInstallUpdateAsync(versionInfo);
                }
                else if (isMandatory)
                {
                    // For mandatory updates, keep showing the prompt
                    await Task.Delay(5000);
                    await ShowUpdatePromptAsync(versionInfo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing update prompt: {ex.Message}");
            }
        }

        /// <summary>
        /// Download and install the update
        /// </summary>
        private async Task DownloadAndInstallUpdateAsync(VersionCheckResponseDto versionInfo)
        {
            try
            {
                var mainPage = Application.Current?.Windows[0]?.Page;
                if (mainPage == null) return;

                // Create a simple progress indicator
                var progress = new Progress<double>(percent =>
                {
                    var percentInt = (int)(percent * 100);
                    System.Diagnostics.Debug.WriteLine($"Download progress: {percentInt}%");
                });

                // Start download
                var (success, message) = await _versionCheckService.DownloadAndInstallUpdateAsync(versionInfo, progress);

                if (success)
                {
                    await mainPage.DisplayAlert(
                        "Update Ready",
                        "The update has been downloaded. Please complete the installation.",
                        "OK");
                }
                else
                {
                    await mainPage.DisplayAlert(
                        "Update Failed",
                        $"Failed to download update: {message}",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error downloading update: {ex.Message}");
                var mainPage = Application.Current?.Windows[0]?.Page;
                if (mainPage != null)
                {
                    await mainPage.DisplayAlert(
                        "Error",
                        $"An error occurred while downloading the update: {ex.Message}",
                        "OK");
                }
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