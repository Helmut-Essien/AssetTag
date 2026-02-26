using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileData.Data;
using Microsoft.EntityFrameworkCore;
using MobileApp.Services;
using System.Collections.ObjectModel;

namespace MobileApp.ViewModels
{
    /// <summary>
    /// ViewModel for the Inventory List screen with offline sync capabilities
    /// </summary>
    public partial class InventoryViewModel : BaseViewModel
    {
        private readonly LocalDbContext _dbContext;
        private readonly IAuthService _authService;
        private readonly IAssetService _assetService;
        private readonly ISyncService _syncService;

        [ObservableProperty]
        private ObservableCollection<AssetItemViewModel> assets = new();

        [ObservableProperty]
        private ObservableCollection<AssetItemViewModel> filteredAssets = new();

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private bool isAllFilterActive = true;

        [ObservableProperty]
        private bool isPendingSyncFilterActive = false;

        [ObservableProperty]
        private bool hasAssets = false;

        [ObservableProperty]
        private bool showEmptyState = false;

        [ObservableProperty]
        private string emptyStateMessage = "Your inventory is empty. Tap '+' to add one!";

        [ObservableProperty]
        private string selectedCategory = "All Categories";

        [ObservableProperty]
        private string selectedLocation = "All Locations";

        [ObservableProperty]
        private string selectedSyncStatus = "All Status";

        [ObservableProperty]
        private string currentSortOption = "Name (A-Z)";

        [ObservableProperty]
        private int pendingSyncCount;

        [ObservableProperty]
        private bool hasPendingSync;

        public InventoryViewModel(
            LocalDbContext dbContext,
            IAuthService authService,
            IAssetService assetService,
            ISyncService syncService)
        {
            _dbContext = dbContext;
            _authService = authService;
            _assetService = assetService;
            _syncService = syncService;
            Title = "Inventory";
        }

        /// <summary>
        /// Load assets from the database
        /// </summary>
        [RelayCommand]
        public async Task LoadAssetsAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;

                // Validate token before loading data
                if (!await ValidateTokenAsync(_authService))
                {
                    return;
                }

                // Load assets using AssetService
                var assetsList = await _assetService.GetAllAssetsAsync();

                Assets.Clear();
                foreach (var asset in assetsList)
                {
                    Assets.Add(new AssetItemViewModel
                    {
                        AssetId = asset.AssetId,
                        Name = asset.Name,
                        AssetTag = asset.AssetTag,
                        CategoryName = asset.Category?.Name ?? "Unknown",
                        CategoryIcon = GetCategoryIcon(asset.Category?.Name),
                        LocationName = asset.Location?.Name ?? "Unknown",
                        IsPendingSync = await IsPendingSyncAsync(asset.AssetId),
                        DateModified = asset.DateModified
                    });
                }

                // Apply current filters
                ApplyFilters();

                HasAssets = Assets.Count > 0;
                ShowEmptyState = FilteredAssets.Count == 0;
                
                if (ShowEmptyState && !string.IsNullOrEmpty(SearchText))
                {
                    EmptyStateMessage = "No assets match your search";
                }
                else if (ShowEmptyState && (IsPendingSyncFilterActive ||
                         SelectedCategory != "All Categories" ||
                         SelectedLocation != "All Locations"))
                {
                    EmptyStateMessage = "No assets match your filters";
                }
                else
                {
                    EmptyStateMessage = "Your inventory is empty. Tap '+' to add one!";
                }

                // Update sync status
                await UpdateSyncStatusAsync();

                // Try background sync (fire-and-forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _syncService.FullSyncAsync();
                        // Update sync status after background sync
                        await MainThread.InvokeOnMainThreadAsync(async () => await UpdateSyncStatusAsync());
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Background sync failed: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading assets: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "Failed to load assets. Please try again.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Check if an asset has pending sync operations
        /// </summary>
        private async Task<bool> IsPendingSyncAsync(string assetId)
        {
            return await _dbContext.SyncQueue
                .AnyAsync(s => s.EntityId == assetId && s.EntityType == "Asset");
        }

        /// <summary>
        /// Update sync status badge
        /// </summary>
        private async Task UpdateSyncStatusAsync()
        {
            PendingSyncCount = await _syncService.GetPendingSyncCountAsync();
            HasPendingSync = PendingSyncCount > 0;
        }

        /// <summary>
        /// Manual sync command - triggered by user
        /// </summary>
        [RelayCommand]
        private async Task ManualSyncAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;

                var (success, message) = await _syncService.FullSyncAsync();
                
                // Always update sync status to reflect current state
                await UpdateSyncStatusAsync();
                
                await Shell.Current.DisplayAlert(
                    success ? "Sync Complete" : "Sync Error",
                    message,
                    "OK");

                // Reload assets only if sync was successful
                if (success)
                {
                    await LoadAssetsAsync();
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Sync failed: {ex.Message}", "OK");
                // Update status even on exception to show accurate pending count
                await UpdateSyncStatusAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Apply search and filter logic
        /// </summary>
        private void ApplyFilters()
        {
            var filtered = Assets.AsEnumerable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLower();
                filtered = filtered.Where(a =>
                    a.Name.ToLower().Contains(searchLower) ||
                    a.AssetTag.ToLower().Contains(searchLower) ||
                    (a.LocationName?.ToLower().Contains(searchLower) ?? false));
            }

            // Apply pending sync filter
            if (IsPendingSyncFilterActive)
            {
                filtered = filtered.Where(a => a.IsPendingSync);
            }

            // Apply category filter
            if (SelectedCategory != "All Categories")
            {
                filtered = filtered.Where(a => a.CategoryName == SelectedCategory);
            }

            // Apply location filter
            if (SelectedLocation != "All Locations")
            {
                filtered = filtered.Where(a => a.LocationName == SelectedLocation);
            }

            // Apply sync status filter
            if (SelectedSyncStatus == "Synced")
            {
                filtered = filtered.Where(a => !a.IsPendingSync);
            }
            else if (SelectedSyncStatus == "Pending")
            {
                filtered = filtered.Where(a => a.IsPendingSync);
            }

            // Apply sorting
            filtered = CurrentSortOption switch
            {
                "Name (A-Z)" => filtered.OrderBy(a => a.Name),
                "Name (Z-A)" => filtered.OrderByDescending(a => a.Name),
                "Date Modified (Newest)" => filtered.OrderByDescending(a => a.DateModified),
                "Date Modified (Oldest)" => filtered.OrderBy(a => a.DateModified),
                "Status (Synced First)" => filtered.OrderBy(a => a.IsPendingSync),
                "Status (Pending First)" => filtered.OrderByDescending(a => a.IsPendingSync),
                _ => filtered.OrderBy(a => a.Name)
            };

            FilteredAssets.Clear();
            foreach (var asset in filtered)
            {
                FilteredAssets.Add(asset);
            }

            ShowEmptyState = FilteredAssets.Count == 0;
        }

        /// <summary>
        /// Handle search text changes
        /// </summary>
        partial void OnSearchTextChanged(string value)
        {
            ApplyFilters();
        }

        /// <summary>
        /// Toggle All filter
        /// </summary>
        [RelayCommand]
        private void ToggleAllFilter()
        {
            IsAllFilterActive = true;
            IsPendingSyncFilterActive = false;
            SelectedCategory = "All Categories";
            SelectedLocation = "All Locations";
            SelectedSyncStatus = "All Status";
            ApplyFilters();
        }

        /// <summary>
        /// Toggle Pending Sync filter
        /// </summary>
        [RelayCommand]
        private void TogglePendingSyncFilter()
        {
            IsPendingSyncFilterActive = !IsPendingSyncFilterActive;
            IsAllFilterActive = false;
            ApplyFilters();
        }

        /// <summary>
        /// Show sort options
        /// </summary>
        [RelayCommand]
        private async Task ShowSortOptionsAsync()
        {
            var action = await Shell.Current.DisplayActionSheet(
                "Sort By",
                "Cancel",
                null,
                "Name (A-Z)",
                "Name (Z-A)",
                "Date Modified (Newest)",
                "Date Modified (Oldest)",
                "Status (Synced First)",
                "Status (Pending First)");

            if (action != null && action != "Cancel")
            {
                CurrentSortOption = action;
                ApplyFilters();
            }
        }

        /// <summary>
        /// Show advanced filter options
        /// </summary>
        [RelayCommand]
        private async Task ShowAdvancedFiltersAsync()
        {
            // TODO: Implement bottom sheet with advanced filters
            // For now, show a simple action sheet
            var categories = await _dbContext.Categories.Select(c => c.Name).ToListAsync();
            var locations = await _dbContext.Locations.Select(l => l.Name).ToListAsync();

            await Shell.Current.DisplayAlert(
                "Advanced Filters",
                "Advanced filter options will be available in the next update.",
                "OK");
        }

        /// <summary>
        /// Clear all filters
        /// </summary>
        [RelayCommand]
        private void ClearFilters()
        {
            SearchText = string.Empty;
            IsAllFilterActive = true;
            IsPendingSyncFilterActive = false;
            SelectedCategory = "All Categories";
            SelectedLocation = "All Locations";
            SelectedSyncStatus = "All Status";
            ApplyFilters();
        }

        /// <summary>
        /// Navigate to asset details
        /// </summary>
        [RelayCommand]
        private async Task ViewAssetDetailsAsync(AssetItemViewModel asset)
        {
            if (asset == null) return;

            try
            {
                // TODO: Navigate to asset details page when implemented
                await Shell.Current.DisplayAlert(
                    "Asset Details",
                    $"Opening details for {asset.Name}...",
                    "OK");
                // await Shell.Current.GoToAsync($"AssetDetailsPage?assetId={asset.AssetId}");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        /// <summary>
        /// Navigate to add asset page
        /// </summary>
        [RelayCommand]
        private async Task AddAssetAsync()
        {
            try
            {
                // TODO: Navigate to add asset page when implemented
                await Shell.Current.DisplayAlert(
                    "Add Asset",
                    "Opening asset registration form...",
                    "OK");
                // await Shell.Current.GoToAsync("AddAssetPage");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        /// <summary>
        /// Get category icon based on category name
        /// </summary>
        private string GetCategoryIcon(string? categoryName)
        {
            if (string.IsNullOrEmpty(categoryName))
                return "📦";

            return categoryName.ToLower() switch
            {
                var c when c.Contains("laptop") || c.Contains("computer") => "💻",
                var c when c.Contains("furniture") || c.Contains("chair") || c.Contains("desk") => "🪑",
                var c when c.Contains("printer") => "🖨️",
                var c when c.Contains("phone") || c.Contains("mobile") => "📱",
                var c when c.Contains("monitor") || c.Contains("screen") || c.Contains("display") => "🖥️",
                var c when c.Contains("tool") => "🔧",
                var c when c.Contains("vehicle") || c.Contains("car") => "🚗",
                var c when c.Contains("camera") => "📷",
                var c when c.Contains("network") || c.Contains("router") => "🌐",
                var c when c.Contains("server") => "🖧",
                _ => "📦"
            };
        }

        /// <summary>
        /// Refresh the inventory list
        /// </summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadAssetsAsync();
        }
    }

    /// <summary>
    /// ViewModel for individual asset items in the list
    /// </summary>
    public partial class AssetItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string assetId = string.Empty;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string assetTag = string.Empty;

        [ObservableProperty]
        private string categoryName = string.Empty;

        [ObservableProperty]
        private string categoryIcon = "📦";

        [ObservableProperty]
        private string locationName = string.Empty;

        [ObservableProperty]
        private bool isPendingSync = false;

        [ObservableProperty]
        private DateTime dateModified;

        public string DisplayTag => $"ID: #{AssetTag}";
        public string DisplayLocation => $"📍 {LocationName}";
        public string SyncStatusColor => IsPendingSync ? "#FFC107" : "Transparent";
    }
}