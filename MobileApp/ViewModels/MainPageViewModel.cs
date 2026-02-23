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
        private readonly LocalDbContext _dbContext;
        private readonly IAuthService _authService;

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

        public MainPageViewModel(LocalDbContext dbContext, IAuthService authService)
        {
            _dbContext = dbContext;
            _authService = authService;
            Title = "Asset Management";
        }

        /// <summary>
        /// Load dashboard statistics from the database
        /// </summary>
        [RelayCommand]
        public async Task LoadDashboardDataAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;

                // Load total assets count
                TotalAssets = await _dbContext.Assets.CountAsync();

                // Load assets scanned today
                ScannedToday = await _dbContext.Assets
                    .Where(a => a.DateModified.Date == DateTime.Today)
                    .CountAsync();

                // Load pending sync count
                // TODO: Add IsSynced property to Asset model or use alternative sync tracking
                // For now, we'll set it to 0 as a placeholder
                PendingSync = 0;

                // Load categories count
                Categories = await _dbContext.Categories.CountAsync();

                // Update last sync time if available
                // TODO: Store last sync time in preferences or database
                var lastSyncTime = Preferences.Get("LastSyncTime", string.Empty);
                if (!string.IsNullOrEmpty(lastSyncTime))
                {
                    if (DateTime.TryParse(lastSyncTime, out var syncDate))
                    {
                        LastSync = syncDate.ToString("MMM dd, yyyy HH:mm");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Error loading dashboard data: {ex.Message}");
                
                // Set default values
                TotalAssets = 0;
                ScannedToday = 0;
                PendingSync = 0;
                Categories = 0;
            }
            finally
            {
                IsBusy = false;
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

                // TODO: Implement actual sync logic with API
                await Shell.Current.DisplayAlert("Sync Data", "Syncing with server...", "OK");

                // Simulate sync delay
                await Task.Delay(1000);

                // Update last sync time
                var now = DateTime.Now;
                Preferences.Set("LastSyncTime", now.ToString("O"));
                LastSync = now.ToString("MMM dd, yyyy HH:mm");

                // Reload dashboard data to reflect sync changes
                await LoadDashboardDataAsync();

                await Shell.Current.DisplayAlert("Success", "Data synced successfully!", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Sync failed: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
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
        /// Navigate to settings page (bottom nav)
        /// </summary>
        [RelayCommand]
        private async Task NavigateToSettingsAsync()
        {
            try
            {
                // Check if biometric is available
                var biometricAvailable = await BiometricAuthentication.IsBiometricAvailableAsync();
                var biometricEnabled = await _authService.IsBiometricEnabledAsync();

                string biometricStatus = biometricAvailable
                    ? (biometricEnabled ? "✅ Enabled" : "❌ Disabled")
                    : "⚠️ Not Available";

                var action = await Shell.Current.DisplayActionSheet(
                    "Settings",
                    "Cancel",
                    null,
                    biometricAvailable ? $"Biometric Login: {biometricStatus}" : null,
                    "Logout");

                if (action == "Logout")
                {
                    await LogoutAsync();
                }
                else if (action != null && action.StartsWith("Biometric"))
                {
                    await ToggleBiometricAsync();
                }
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
                    
                    // If no stored credentials, ask for them
                    if (string.IsNullOrEmpty(storedEmail) || string.IsNullOrEmpty(storedPassword))
                    {
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
                    // Navigate back to login page
                    await Shell.Current.GoToAsync($"/{nameof(LoginPage)}");
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