using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileData.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MobileApp.Services;
using MobileApp.Views;

namespace MobileApp.ViewModels
{
    /// <summary>
    /// ViewModel for the main dashboard page
    /// </summary>
    public partial class MainPageViewModel : BaseViewModel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IAuthService _authService;
        private readonly ISyncService _syncService;
        private readonly IAssetService _assetService;
        private readonly ILogger<MainPageViewModel> _logger;

        [ObservableProperty]
        private int totalAssets;

        [ObservableProperty]
        private int scannedToday;

        [ObservableProperty]
        private int pendingSync;

        [ObservableProperty]
        private int categories;

        [ObservableProperty]
        private string lastSync = "Never synced";

        [ObservableProperty]
        private bool isSyncing;

        [ObservableProperty]
        private double syncProgress;

        [ObservableProperty]
        private string syncStatusText = "";

        public MainPageViewModel(
            IServiceProvider serviceProvider,
            IAuthService authService,
            ISyncService syncService,
            IAssetService assetService,
            ILogger<MainPageViewModel> logger)
        {
            _serviceProvider = serviceProvider;
            _authService = authService;
            _syncService = syncService;
            _assetService = assetService;
            _logger = logger;
            Title = "Asset Management";
            
            // CRITICAL: Start with IsBusy = false to show cached content immediately
            // This prevents black screen on rapid tab switches
            IsBusy = false;
        }

        /// <summary>
        /// Load dashboard statistics from the database
        /// OPTIMIZED: All queries run in parallel and use ConfigureAwait(false)
        /// </summary>
        [RelayCommand]
        public async Task LoadDashboardDataAsync()
        {
            try
            {
                IsBusy = true;
                
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

                // PERFORMANCE: Run all queries in parallel to reduce total load time
                var totalAssetsTask = dbContext.Assets
                    .AsNoTracking()
                    .CountAsync();

                var scannedTodayTask = dbContext.Assets
                    .AsNoTracking()
                    .Where(a => a.LastScannedAt.HasValue && a.LastScannedAt.Value.Date == DateTime.Today)
                    .CountAsync();

                var pendingSyncTask = _syncService.GetPendingSyncCountAsync();

                var categoriesTask = dbContext.Categories
                    .AsNoTracking()
                    .CountAsync();

                var deviceInfoTask = dbContext.DeviceInfo
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                // Wait for all queries to complete in parallel
                await Task.WhenAll(
                    totalAssetsTask,
                    scannedTodayTask,
                    pendingSyncTask,
                    categoriesTask,
                    deviceInfoTask
                );

                // Update properties on UI thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    TotalAssets = totalAssetsTask.Result;
                    ScannedToday = scannedTodayTask.Result;
                    PendingSync = pendingSyncTask.Result;
                    Categories = categoriesTask.Result;

                    var deviceInfo = deviceInfoTask.Result;
                    if (deviceInfo != null && deviceInfo.LastSync > DateTime.MinValue)
                    {
                        var timeSinceSync = DateTime.UtcNow - deviceInfo.LastSync;
                        if (timeSinceSync.TotalMinutes < 1)
                            LastSync = "Just now";
                        else if (timeSinceSync.TotalHours < 1)
                            LastSync = $"{(int)timeSinceSync.TotalMinutes} min ago";
                        else if (timeSinceSync.TotalDays < 1)
                            LastSync = $"{(int)timeSinceSync.TotalHours} hours ago";
                        else
                            LastSync = deviceInfo.LastSync.ToString("MMM dd, yyyy HH:mm");
                    }
                    else
                    {
                        LastSync = "Never synced";
                    }
                });
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Error loading dashboard data: {ex.Message}");
                
                // Set default values on UI thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    TotalAssets = 0;
                    ScannedToday = 0;
                    PendingSync = 0;
                    Categories = 0;
                    LastSync = "Error loading";
                });
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Scan asset barcode/QR code and navigate to update page if exists, or show not found message
        /// </summary>
        [RelayCommand]
        private async Task ScanAssetAsync()
        {
            try
            {
                // Check camera permission
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                    if (status != PermissionStatus.Granted)
                    {
                        await Shell.Current.DisplayAlert(
                            "Permission Denied",
                            "Camera permission is required to scan barcodes. Please enable it in settings.",
                            "OK");
                        return;
                    }
                }

                // Create and navigate to scanner page
                var scannerPage = new Views.BarcodeScannerPage();
                await Shell.Current.Navigation.PushModalAsync(scannerPage);

                // Wait for scan result
                var scannedValue = await scannerPage.GetScanResultAsync();

                _logger.LogInformation("Scan completed. Scanned value: '{ScannedValue}'", scannedValue ?? "NULL");

                // Give extra time for modal to fully close and UI to settle
                await Task.Delay(500);

                if (!string.IsNullOrWhiteSpace(scannedValue))
                {
                    _logger.LogInformation("Processing scanned value: {ScannedValue}", scannedValue);
                    // Search for asset by digital asset tag or asset tag
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<MobileData.Data.LocalDbContext>();
                    
                    // Find asset with AsNoTracking for read-only access
                    var asset = await dbContext.Assets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.DigitalAssetTag == scannedValue || a.AssetTag == scannedValue);

                    if (asset != null)
                    {
                        // Update LastScannedAt using raw SQL to avoid triggering sync queue
                        // This is a read-only tracking field that shouldn't create pending changes
                        try
                        {
                            await dbContext.Database.ExecuteSqlRawAsync(
                                "UPDATE Assets SET LastScannedAt = {0} WHERE AssetId = {1}",
                                DateTime.UtcNow, asset.AssetId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to update LastScannedAt for asset {AssetId}", asset.AssetId);
                            // Continue to navigation even if update fails
                        }
                        
                        // Asset found - navigate to view/edit page
                        await Shell.Current.GoToAsync($"{nameof(AddAssetPage)}?assetId={asset.AssetId}");
                    }
                    else
                    {
                        // Asset not found
                        await Shell.Current.DisplayAlert(
                            "Asset Not Found",
                            $"No asset found with tag: {scannedValue}",
                            "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to scan asset: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Navigate to add new asset page
        /// </summary>
        [RelayCommand]
        private async Task AddAssetAsync()
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(AddAssetPage));
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to navigate: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Navigate to assets list page
        /// </summary>
        [RelayCommand]
        private async Task ViewAssetsAsync()
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(InventoryPage));
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        /// <summary>
        /// Sync local data with the server with progress tracking
        /// </summary>
        [RelayCommand]
        private async Task SyncDataAsync()
        {
            if (IsBusy || IsSyncing) return;

            try
            {
                IsBusy = true;
                IsSyncing = true;
                SyncProgress = 0;
                SyncStatusText = "Preparing sync...";

                // Simulate progress updates during sync
                var progressTask = Task.Run(async () =>
                {
                    for (int i = 0; i <= 90; i += 10)
                    {
                        if (!IsSyncing) break;
                        
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            SyncProgress = i / 100.0;
                            if (i < 30)
                                SyncStatusText = "Pushing local changes...";
                            else if (i < 60)
                                SyncStatusText = "Pulling server updates...";
                            else
                                SyncStatusText = "Finalizing sync...";
                        });
                        
                        await Task.Delay(300);
                    }
                });

                // Perform full bidirectional sync (enqueue to central sync queue)
                var (success, message) = await _syncService.EnqueueFullSyncAsync();

                // Complete progress
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SyncProgress = 1.0;
                    SyncStatusText = success ? "Sync complete!" : "Sync failed";
                });

                await Task.Delay(500); // Brief pause to show completion

                // Show result to user
                await Shell.Current.DisplayAlert(
                    success ? "Success" : "Sync Error",
                    message,
                    "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Sync failed: {ex.Message}", "OK");
            }
            finally
            {
                IsSyncing = false;
                SyncProgress = 0;
                SyncStatusText = "";
                IsBusy = false;
                
                // Reload dashboard data AFTER IsBusy is set to false and dialog is dismissed
                // This ensures the pending count is refreshed after successful sync
                await LoadDashboardDataAsync();
            }
        }

        /// <summary>
        /// Refresh dashboard data (for home button tap)
        /// </summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadDashboardDataAsync();
        }

        /// <summary>
        /// Reset sync state to force full re-sync from server
        /// </summary>
        [RelayCommand]
        private async Task ResetSyncStateAsync()
        {
            if (IsBusy) return;

            try
            {
                var confirm = await Shell.Current.DisplayAlert(
                    "Reset Sync State",
                    "This will reset the sync timestamp and fetch ALL data from the server on next sync. This is useful if you're missing data. Continue?",
                    "Yes, Reset",
                    "Cancel");

                if (!confirm)
                    return;

                IsBusy = true;

                // Reset sync state
                await _syncService.ResetSyncStateAsync();

                // Perform full sync immediately (enqueue to central sync queue)
                var (success, message) = await _syncService.EnqueueFullSyncAsync();

                // Show result
                await Shell.Current.DisplayAlert(
                    success ? "Success" : "Sync Error",
                    success ? "Sync state reset and full sync completed successfully!" : $"Reset completed but sync failed: {message}",
                    "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Reset failed: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
                
                // Reload dashboard data AFTER IsBusy is set to false and dialog is dismissed
                // This ensures the pending count is refreshed after successful sync
                await LoadDashboardDataAsync();
            }
        }

        /// <summary>
        /// Navigate to assets page (bottom nav)
        /// </summary>
        [RelayCommand]
        private async Task NavigateToAssetsAsync()
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(InventoryPage));
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        /// <summary>
        /// Navigate to reports page (bottom nav)
        /// </summary>
        [RelayCommand]
        private async Task NavigateToReportsAsync()
        {
            try
            {
                // TODO: Navigate to reports page when implemented
                await Shell.Current.DisplayAlert("Reports", "Opening reports page...", "OK");
                // await Shell.Current.GoToAsync("ReportsPage");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        /// <summary>
        /// Navigate to settings page
        /// </summary>
        [RelayCommand]
        private async Task NavigateToSettingsAsync()
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(SettingsPage));
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        /// <summary>
        /// Toggle biometric authentication on/off
        /// </summary>
        [RelayCommand]
        private async Task ToggleBiometricAsync()
        {
            try
            {
                var biometricEnabled = await _authService.IsBiometricEnabledAsync();

                if (biometricEnabled)
                {
                    // Disable biometric
                    var confirm = await Shell.Current.DisplayAlert(
                        "Disable Biometric Login",
                        "Are you sure you want to disable biometric authentication?",
                        "Yes",
                        "No");

                    if (confirm)
                    {
                        await _authService.DisableBiometricAuthenticationAsync();
                        await Shell.Current.DisplayAlert(
                            "Success",
                            "Biometric authentication has been disabled.",
                            "OK");
                    }
                }
                else
                {
                    // Enable biometric - check if biometric is available first
                    var biometricAvailable = await BiometricAuthentication.IsBiometricAvailableAsync();
                    
                    if (!biometricAvailable)
                    {
                        await Shell.Current.DisplayAlert(
                            "Not Available",
                            "Biometric authentication is not available on this device. Please ensure you have set up fingerprint or face recognition in your device settings.",
                            "OK");
                        return;
                    }

                    // Check if user has stored credentials from login
                    var (storedEmail, storedPassword) = await _authService.GetStoredCredentialsAsync();
                    
                    // If no stored biometric credentials, try to use current session credentials
                    if (string.IsNullOrEmpty(storedEmail) || string.IsNullOrEmpty(storedPassword))
                    {
                        var (sessionEmail, sessionPassword) = await _authService.GetCurrentSessionCredentialsAsync();
                        
                        if (!string.IsNullOrEmpty(sessionEmail) && !string.IsNullOrEmpty(sessionPassword))
                        {
                            // Use session credentials
                            storedEmail = sessionEmail;
                            storedPassword = sessionPassword;
                        }
                        else
                        {
                            // No session credentials available, ask for them
                            var email = await Shell.Current.DisplayPromptAsync(
                                "Enable Biometric Login",
                                "Enter your email address:",
                                "Next",
                                "Cancel",
                                keyboard: Keyboard.Email);

                            if (string.IsNullOrWhiteSpace(email))
                                return;

                            var password = await Shell.Current.DisplayPromptAsync(
                                "Enable Biometric Login",
                                "Enter your password:",
                                "Enable",
                                "Cancel",
                                keyboard: Keyboard.Default);

                            if (string.IsNullOrWhiteSpace(password))
                                return;

                            // Verify credentials by attempting login
                            IsBusy = true;
                            var (loginSuccess, _, loginMessage) = await _authService.LoginAsync(email, password);
                            IsBusy = false;

                            if (!loginSuccess)
                            {
                                await Shell.Current.DisplayAlert(
                                    "Error",
                                    $"Invalid credentials: {loginMessage}",
                                    "OK");
                                return;
                            }

                            storedEmail = email;
                            storedPassword = password;
                        }
                    }

                    // Authenticate with biometrics to confirm
                    var authRequest = new AuthenticationRequest
                    {
                        Title = "Enable Biometric Login",
                        Reason = "Scan your fingerprint or face to enable biometric login",
                        AllowAlternativeAuthentication = true
                    };

                    var result = await BiometricAuthentication.AuthenticateAsync(authRequest);

                    if (result.Authenticated)
                    {
                        await _authService.EnableBiometricAuthenticationAsync(storedEmail, storedPassword);
                        await Shell.Current.DisplayAlert(
                            "Success",
                            "Biometric authentication has been enabled! You can now login using biometrics even if your session expires.",
                            "OK");
                    }
                    else
                    {
                        await Shell.Current.DisplayAlert(
                            "Failed",
                            string.IsNullOrEmpty(result.ErrorMessage)
                                ? "Biometric authentication failed. Please try again."
                                : $"Biometric authentication failed: {result.ErrorMessage}",
                            "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Navigate to categories page (bottom nav)
        /// </summary>
        [RelayCommand]
        private async Task NavigateToCategoriesAsync()
        {
            try
            {
                // TODO: Navigate to categories page when implemented
                await Shell.Current.DisplayAlert("Categories", "Opening categories page...", "OK");
                // await Shell.Current.GoToAsync("CategoriesPage");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        /// <summary>
        /// Navigate to locations page (bottom nav)
        /// </summary>
        [RelayCommand]
        private async Task NavigateToLocationsAsync()
        {
            try
            {
                // TODO: Navigate to locations page when implemented
                await Shell.Current.DisplayAlert("Locations", "Opening locations page...", "OK");
                // await Shell.Current.GoToAsync("LocationsPage");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        /// <summary>
        /// Logout the current user
        /// </summary>
        [RelayCommand]
        private async Task LogoutAsync()
        {
            try
            {
                var confirm = await Shell.Current.DisplayAlert(
                    "Logout",
                    "Are you sure you want to logout?",
                    "Yes",
                    "No");

                if (!confirm)
                    return;

                IsBusy = true;

                var (success, message) = await _authService.LogoutAsync();

                if (success)
                {
                    // Navigate back to login page and hide tabs
                    if (Shell.Current is AppShell appShell)
                    {
                        await appShell.ShowLoginAsync();
                    }
                }
                else
                {
                    await Shell.Current.DisplayAlert("Logout", message, "OK");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Logout failed: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}