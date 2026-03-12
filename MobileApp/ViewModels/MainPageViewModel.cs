using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileData.Data;
using Microsoft.EntityFrameworkCore;
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

        public MainPageViewModel(
            IServiceProvider serviceProvider,
            IAuthService authService,
            ISyncService syncService)
        {
            _serviceProvider = serviceProvider;
            _authService = authService;
            _syncService = syncService;
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
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

                // PERFORMANCE: Run all queries in parallel to reduce total load time
                var totalAssetsTask = dbContext.Assets
                    .AsNoTracking()
                    .CountAsync();

                var scannedTodayTask = dbContext.Assets
                    .AsNoTracking()
                    .Where(a => a.DateModified.Date == DateTime.Today)
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
        }

        /// <summary>
        /// Navigate to barcode/QR scanner page
        /// </summary>
        [RelayCommand]
        private async Task ScanAssetAsync()
        {
            try
            {
                // TODO: Navigate to scanner page when implemented
                await Shell.Current.DisplayAlert("Scan Asset", "Opening scanner...", "OK");
                // await Shell.Current.GoToAsync("ScanPage");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
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
                // TODO: Navigate to add asset page when implemented
                await Shell.Current.DisplayAlert("Add Asset", "Opening asset registration form...", "OK");
                // await Shell.Current.GoToAsync("AddAssetPage");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
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
        /// Sync local data with the server
        /// </summary>
        [RelayCommand]
        private async Task SyncDataAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;

                // Perform full bidirectional sync (enqueue to central sync queue)
                var (success, message) = await _syncService.EnqueueFullSyncAsync();

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