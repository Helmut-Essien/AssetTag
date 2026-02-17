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
                // TODO: Navigate to assets list page when implemented
                await Shell.Current.DisplayAlert("View Assets", "Opening asset inventory...", "OK");
                // await Shell.Current.GoToAsync("AssetsPage");
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
                // TODO: Navigate to assets page when implemented
                await Shell.Current.DisplayAlert("Assets", "Opening assets page...", "OK");
                // await Shell.Current.GoToAsync("AssetsPage");
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
                // TODO: Navigate to settings page when implemented
                await Shell.Current.DisplayAlert("Settings", "Opening settings page...", "OK");
                // await Shell.Current.GoToAsync("SettingsPage");
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