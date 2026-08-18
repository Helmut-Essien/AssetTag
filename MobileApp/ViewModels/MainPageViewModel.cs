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
        private readonly IBarcodeScannerService _barcodeScannerService;
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

        [ObservableProperty]
        private bool isRefreshing;

        private bool _isInitialLoad = true;

        public MainPageViewModel(
            IServiceProvider serviceProvider,
            IAuthService authService,
            ISyncService syncService,
            IAssetService assetService,
            IBarcodeScannerService barcodeScannerService,
            ILogger<MainPageViewModel> logger)
        {
            _serviceProvider = serviceProvider;
            _authService = authService;
            _syncService = syncService;
            _assetService = assetService;
            _barcodeScannerService = barcodeScannerService;
            _logger = logger;
            Title = "Asset Management";
            IsBusy = false;
            _syncService.SyncProgressChanged += OnSyncProgressChanged;
        }

        private void OnSyncProgressChanged(object? sender, SyncProgressEventArgs e)
        {
            SyncProgress = e.NormalizedProgress;
            SyncStatusText = string.IsNullOrWhiteSpace(e.Message)
                ? e.Phase.ToString()
                : e.Message;
        }

        /// <summary>
        /// Load dashboard statistics from the database.
        /// Queries run sequentially — SQLite parallel readers often raise first-chance
        /// SQLITE_BUSY exceptions (retried by EF), which pause "Break on All Exceptions".
        /// </summary>
        [RelayCommand]
        public async Task LoadDashboardDataAsync()
        {
            try
            {
                if (_isInitialLoad)
                    IsBusy = true;

                var today = DateTime.Today;

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

                var total = await db.Assets.AsNoTracking().CountAsync();
                var scanned = await db.Assets.AsNoTracking()
                    .CountAsync(a => a.LastScannedAt.HasValue && a.LastScannedAt.Value.Date == today);
                var categoryCount = await db.Categories.AsNoTracking().CountAsync();
                var deviceInfo = await db.DeviceInfo.AsNoTracking()
                    .OrderBy(d => d.Id)
                    .FirstOrDefaultAsync();
                var pending = await _syncService.GetPendingSyncCountAsync();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    TotalAssets = total;
                    ScannedToday = scanned;
                    PendingSync = pending;
                    Categories = categoryCount;

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
                _isInitialLoad = false;
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
                var scannedValue = await _barcodeScannerService.ScanAsync();

                _logger.LogInformation("Scan completed. Scanned value: '{ScannedValue}'", scannedValue ?? "NULL");

                if (!string.IsNullOrWhiteSpace(scannedValue))
                {
                    _logger.LogInformation("Processing scanned value: {ScannedValue}", scannedValue);
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<MobileData.Data.LocalDbContext>();
                    
                    var asset = await dbContext.Assets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.DigitalAssetTag == scannedValue || a.AssetTag == scannedValue);

                    if (asset != null)
                    {
                        try
                        {
                            await dbContext.Database.ExecuteSqlRawAsync(
                                "UPDATE Assets SET LastScannedAt = {0} WHERE AssetId = {1}",
                                DateTime.UtcNow, asset.AssetId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to update LastScannedAt for asset {AssetId}", asset.AssetId);
                        }
                        
                        await Shell.Current.GoToAsync($"{nameof(AddAssetPage)}?assetId={asset.AssetId}");
                    }
                    else
                    {
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
        /// FIXED: Use absolute route for Shell hierarchy tab navigation
        /// </summary>
        [RelayCommand]
        private async Task ViewAssetsAsync()
        {
            try
            {
                // Navigate to Inventory tab using absolute route
                await Shell.Current.GoToAsync("//MainTabs/Inventory");
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
            if (IsSyncing) return;

            try
            {
                IsSyncing = true;
                SyncProgress = 0;
                SyncStatusText = "Preparing sync...";

                var (success, message) = await _syncService.EnqueueFullSyncAsync();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SyncProgress = success ? 1.0 : SyncProgress;
                    SyncStatusText = success ? "Sync complete!" : "Sync failed";
                });

                await Task.Delay(400);

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
            if (IsRefreshing) return;

            IsRefreshing = true;
            try
            {
                await LoadDashboardDataAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

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

        [RelayCommand]
        private async Task NavigateToLocationsAsync()
        {
            try
            {
                await Shell.Current.GoToAsync("//MainTabs/Locations");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}
