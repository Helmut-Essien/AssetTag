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

        public InventoryViewModel(LocalDbContext dbContext, IAuthService authService)
        {
            _dbContext = dbContext;
            _authService = authService;
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

                // Load assets from database with related data
                var assetsList = await _dbContext.Assets
                    .Include(a => a.Category)
                    .Include(a => a.Location)
                    .Include(a => a.Department)
                    .OrderBy(a => a.Name)
                    .ToListAsync();

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
                        IsPendingSync = false, // TODO: Implement sync tracking
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