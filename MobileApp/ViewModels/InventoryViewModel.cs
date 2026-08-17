using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileData.Data;
using Microsoft.EntityFrameworkCore;
using MobileApp.Services;
using MauiIcons.Material;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SharedLocation = Shared.Models.Location;

namespace MobileApp.ViewModels
{
    /// <summary>
    /// ViewModel for the Inventory List screen with offline sync capabilities
    /// </summary>
    public partial class InventoryViewModel : BaseViewModel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IAuthService _authService;
        private readonly IAssetService _assetService;
        private readonly ILocationService _locationService;
        private readonly ISyncService _syncService;
        private readonly IBarcodeScannerService _barcodeScannerService;
        [ObservableProperty]
        private ObservableCollection<AssetItemViewModel> assets = new();

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

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private bool isInitialLoad = true;

        [ObservableProperty]
        private string filterChipLabel = "Filter";

        [ObservableProperty]
        private bool isLoadingMore = false;

        [ObservableProperty]
        private bool isFilterPickerOpen;

        [ObservableProperty]
        private string filterPickerTitle = "Filter";

        [ObservableProperty]
        private string filterPickerSearchPlaceholder = "Search...";

        [ObservableProperty]
        private string filterPickerSearchText = string.Empty;

        [ObservableProperty]
        private List<FilterOption> filterPickerItems = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasFilteredFilterOptions))]
        private List<FilterOption> filteredFilterPickerItems = new();

        public bool HasFilteredFilterOptions => FilteredFilterPickerItems.Count > 0;

        private enum FilterPickerKind { Category, Location }
        private FilterPickerKind _filterPickerKind;

        // Separate flag to prevent concurrent loads without blocking the very first call.
        // IsBusy cannot be used for this because the page sets it to true BEFORE calling
        // LoadAssetsAsync (so the skeleton shows), which would cause the old guard to bail out.
        private bool _isLoading = false;
        private bool _reloadRequested = false;
        private bool _suppressSearchReload = false;
        private int _pageIndex = 0;
        private const int PageSize = 50;
        private bool _hasMoreItems = true;
        private HashSet<string> _pendingSyncIds = new();
        private CancellationTokenSource? _searchCts;

        public InventoryViewModel(
            IServiceProvider serviceProvider,
            IAuthService authService,
            IAssetService assetService,
            ILocationService locationService,
            ISyncService syncService,
            IBarcodeScannerService barcodeScannerService)
        {
            _serviceProvider = serviceProvider;
            _authService = authService;
            _assetService = assetService;
            _locationService = locationService;
            _syncService = syncService;
            _barcodeScannerService = barcodeScannerService;
            Title = "Inventory";
            
            // Start with IsBusy = true so skeleton shows immediately when page appears
            // This is set in constructor so it's already true when data binding occurs
            IsBusy = true;
        }

        /// <summary>
        /// Load assets from the database
        /// </summary>
        [RelayCommand]
        public async Task LoadAssetsAsync()
        {
            // Guard against concurrent loads only. Do NOT use IsBusy here because the
            // page sets IsBusy = true before calling this method (to show the skeleton),
            // and using IsBusy as the guard would cause this method to bail immediately.
            if (_isLoading)
            {
                _reloadRequested = true;
                return;
            }

            try
            {
                _isLoading = true;
                
                // Only show skeleton on initial load
                if (IsInitialLoad)
                {
                    IsBusy = true;
                    UpdateVisibilityState();
                }

                // Reset paging state and pending sync IDs
                _pageIndex = 0;
                _hasMoreItems = true;

                // Load pending sync IDs using scoped DbContext with AsNoTracking
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
                    _pendingSyncIds = await dbContext.SyncQueue
                        .AsNoTracking()
                        .Where(s => s.EntityType == "Asset")
                        .Select(s => s.EntityId)
                        .ToHashSetAsync();
                }

                // Load first page (awaits UI update before computing visibility)
                await LoadNextPageAsync(reset: true);

                // Update sync status (non-blocking)
                await UpdateSyncStatusAsync();

                // Token validation moved to background - don't block UI
                _ = Task.Run(async () =>
                {
                    var tokenValid = await TryValidateTokenSilentAsync(_authService);
                    if (!tokenValid)
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await NavigateToLoginAsync();
                        });
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
                _isLoading = false;
                IsBusy = false;
                IsInitialLoad = false;
                UpdateVisibilityState(); // Mark initial load as complete

                if (_reloadRequested)
                {
                    _reloadRequested = false;
                    await LoadAssetsAsync();
                }
            }
        }

        [RelayCommand]
        public async Task LoadMoreAsync()
        {
            if (IsLoadingMore || !_hasMoreItems || _isLoading) return;
            
            try
            {
                IsLoadingMore = true;
                await LoadNextPageAsync(reset: false);
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private async Task LoadNextPageAsync(bool reset)
        {
            try
            {
                bool? pendingOnly = null;
                if (IsPendingSyncFilterActive || SelectedSyncStatus == "Pending")
                    pendingOnly = true;
                else if (SelectedSyncStatus == "Synced")
                    pendingOnly = false;

                var page = await _assetService.GetAssetsPageAsync(
                    _pageIndex,
                    PageSize,
                    searchText: SearchText,
                    categoryName: SelectedCategory,
                    locationName: SelectedLocation,
                    pendingSyncOnly: pendingOnly,
                    sortOption: CurrentSortOption,
                    pendingSyncIds: _pendingSyncIds);

                if (page == null || page.Count == 0)
                {
                    _hasMoreItems = false;
                    if (reset)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            Assets = new ObservableCollection<AssetItemViewModel>();
                            UpdateVisibilityState();
                        });
                    }
                    return;
                }

                // Map off the UI thread
                var newItems = await Task.Run(() =>
                {
                    var list = new List<AssetItemViewModel>(page.Count);
                    foreach (var asset in page)
                    {
                        list.Add(new AssetItemViewModel(OnAssetTapped)
                        {
                            AssetId = asset.AssetId,
                            Name = asset.Name,
                            AssetTag = asset.AssetTag,
                            DigitalAssetTag = asset.DigitalAssetTag,
                            CategoryName = asset.Category?.Name ?? "Unknown",
                            CategoryIcon = GetCategoryIcon(asset.Category?.Name),
                            LocationName = asset.Location?.Name ?? "Unknown",
                            IsPendingSync = _pendingSyncIds.Contains(asset.AssetId),
                            DateModified = asset.DateModified
                        });
                    }

                    return list;
                });

                // Await UI update so HasAssets/ShowEmptyState see the new items
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (reset)
                    {
                        Assets = new ObservableCollection<AssetItemViewModel>(newItems);
                    }
                    else
                    {
                        foreach (var item in newItems)
                            Assets.Add(item);
                    }

                    UpdateVisibilityState();
                });

                if (page.Count < PageSize)
                {
                    _hasMoreItems = false;
                }
                else
                {
                    _pageIndex++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading assets page: {ex.Message}");
            }
        }

        private void UpdateVisibilityState()
        {
            HasAssets = Assets.Count > 0;
            ShowEmptyState = !IsBusy && Assets.Count == 0;
            UpdateFilterChipLabel();

            if (ShowEmptyState && !string.IsNullOrEmpty(SearchText))
            {
                EmptyStateMessage = "No assets match your search";
            }
            else if (ShowEmptyState && (IsPendingSyncFilterActive ||
                     SelectedCategory != "All Categories" ||
                     SelectedLocation != "All Locations" ||
                     SelectedSyncStatus != "All Status"))
            {
                EmptyStateMessage = "No assets match your filters";
            }
            else
            {
                EmptyStateMessage = "Your inventory is empty. Tap '+' to add one!";
            }
        }

        private void UpdateFilterChipLabel()
        {
            var active = 0;
            if (SelectedCategory != "All Categories") active++;
            if (SelectedLocation != "All Locations") active++;
            if (SelectedSyncStatus != "All Status") active++;
            FilterChipLabel = active == 0 ? "Filter" : $"Filter ({active})";
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

                var (success, message) = await _syncService.EnqueueFullSyncAsync();
                
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
        /// Reload from DB when search/filter/sort changes (search is debounced).
        /// </summary>
        private async Task ReloadFromDatabaseAsync(bool debounce)
        {
            if (debounce)
            {
                _searchCts?.Cancel();
                _searchCts = new CancellationTokenSource();
                var token = _searchCts.Token;
                try
                {
                    await Task.Delay(300, token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }

            await LoadAssetsAsync();
        }

        /// <summary>
        /// Handle search text changes
        /// </summary>
        partial void OnSearchTextChanged(string value)
        {
            if (_suppressSearchReload) return;
            _ = ReloadFromDatabaseAsync(debounce: true);
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
            _ = ReloadFromDatabaseAsync(debounce: false);
        }

        /// <summary>
        /// Toggle Pending Sync filter
        /// </summary>
        [RelayCommand]
        private void TogglePendingSyncFilter()
        {
            IsPendingSyncFilterActive = !IsPendingSyncFilterActive;
            IsAllFilterActive = false;
            _ = ReloadFromDatabaseAsync(debounce: false);
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
                await ReloadFromDatabaseAsync(debounce: false);
            }
        }

        /// <summary>
        /// Show advanced filter options
        /// </summary>
        [RelayCommand]
        private async Task ShowAdvancedFiltersAsync()
        {
            var action = await Shell.Current.DisplayActionSheet(
                "Filter inventory",
                "Cancel",
                "Clear Filters",
                "Category",
                "Location",
                "Sync Status");

            if (action == "Cancel" || action == null)
                return;

            if (action == "Clear Filters")
            {
                ClearFilters();
                return;
            }

            if (action == "Category")
            {
                await OpenCategoryPickerAsync();
                return;
            }

            if (action == "Location")
            {
                await OpenLocationPickerAsync();
                return;
            }

            if (action == "Sync Status")
            {
                var selected = await Shell.Current.DisplayActionSheet(
                    "Sync Status",
                    "Cancel",
                    null,
                    "All Status",
                    "Pending",
                    "Synced");
                if (selected != null && selected != "Cancel")
                {
                    SelectedSyncStatus = selected;
                    IsPendingSyncFilterActive = selected == "Pending";
                    ApplySelectedFilters();
                    await ReloadFromDatabaseAsync(debounce: false);
                }
            }
        }

        private async Task OpenCategoryPickerAsync()
        {
            var names = await _assetService.GetCategoryNamesAsync();
            _filterPickerKind = FilterPickerKind.Category;
            FilterPickerTitle = "Select Category";
            FilterPickerSearchPlaceholder = "Search categories...";
            var items = new List<FilterOption>
            {
                new() { Title = "All Categories", Value = "All Categories" }
            };
            items.AddRange(names.Select(name => new FilterOption { Title = name, Value = name }));
            FilterPickerItems = items;
            FilterPickerSearchText = string.Empty;
            ApplyFilterPickerFilter();
            IsFilterPickerOpen = true;
        }

        private async Task OpenLocationPickerAsync()
        {
            var locations = await _locationService.GetAllLocationsAsync();
            _filterPickerKind = FilterPickerKind.Location;
            FilterPickerTitle = "Select Location";
            FilterPickerSearchPlaceholder = "Search by name, campus, building, room...";
            var items = new List<FilterOption>
            {
                new() { Title = "All Locations", Value = "All Locations" }
            };
            items.AddRange(locations
                .OrderBy(location => location.Name)
                .Select(location => new FilterOption
                {
                    Title = location.Name,
                    Subtitle = GetLocationSubtitle(location),
                    Value = location.Name
                }));
            FilterPickerItems = items;
            FilterPickerSearchText = string.Empty;
            ApplyFilterPickerFilter();
            IsFilterPickerOpen = true;
        }

        [RelayCommand]
        private void CloseFilterPicker()
        {
            IsFilterPickerOpen = false;
        }

        [RelayCommand]
        private void SelectFilterOption(FilterOption? option)
        {
            if (option == null) return;

            if (_filterPickerKind == FilterPickerKind.Category)
                SelectedCategory = option.Value;
            else
                SelectedLocation = option.Value;

            IsFilterPickerOpen = false;
            ApplySelectedFilters();
            _ = ReloadFromDatabaseAsync(debounce: false);
        }

        partial void OnFilterPickerSearchTextChanged(string value)
        {
            ApplyFilterPickerFilter();
        }

        private void ApplyFilterPickerFilter()
        {
            var query = FilterPickerSearchText?.Trim();
            IEnumerable<FilterOption> source = FilterPickerItems;

            if (!string.IsNullOrWhiteSpace(query))
            {
                source = source.Where(option =>
                    option.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(option.Subtitle) &&
                     option.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase)));
            }

            FilteredFilterPickerItems = source.ToList();
        }

        private void ApplySelectedFilters()
        {
            IsAllFilterActive = SelectedCategory == "All Categories"
                && SelectedLocation == "All Locations"
                && SelectedSyncStatus == "All Status"
                && !IsPendingSyncFilterActive;
        }

        private static string GetLocationSubtitle(SharedLocation location)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(location.Campus))
                parts.Add(location.Campus);
            if (!string.IsNullOrWhiteSpace(location.Building))
                parts.Add(location.Building);
            if (!string.IsNullOrWhiteSpace(location.Room))
                parts.Add($"Room {location.Room}");
            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Clear search text only (does not reset category/location/sync filters).
        /// </summary>
        [RelayCommand]
        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        /// <summary>
        /// Clear all filters
        /// </summary>
        [RelayCommand]
        private void ClearFilters()
        {
            _suppressSearchReload = true;
            SearchText = string.Empty;
            _suppressSearchReload = false;
            IsAllFilterActive = true;
            IsPendingSyncFilterActive = false;
            SelectedCategory = "All Categories";
            SelectedLocation = "All Locations";
            SelectedSyncStatus = "All Status";
            _ = ReloadFromDatabaseAsync(debounce: false);
        }

        /// <summary>
        /// Callback for when an asset item is tapped (optimized for direct binding)
        /// </summary>
        private void OnAssetTapped(AssetItemViewModel asset)
        {
            // Execute on UI thread
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await ViewAssetDetailsAsync(asset);
            });
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
                // Navigate to AddAssetPage with asset ID for editing
                await Shell.Current.GoToAsync($"AddAssetPage?assetId={asset.AssetId}");
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
                await Shell.Current.GoToAsync(nameof(Views.AddAssetPage));
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to navigate: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Scan barcode to search for asset in inventory
        /// </summary>
        [RelayCommand]
        private async Task ScanToSearchAsync()
        {
            try
            {
                var scannedValue = await _barcodeScannerService.ScanAsync();

                if (!string.IsNullOrWhiteSpace(scannedValue))
                {
                    SearchText = scannedValue;
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to scan barcode: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Get category icon based on category name (Material Design icon enum)
        /// Returns the MaterialIcons enum value for the category.
        /// </summary>
        private MaterialIcons GetCategoryIcon(string? categoryName)
        {
            if (string.IsNullOrEmpty(categoryName))
                return MaterialIcons.Inventory; // Default

            var lower = categoryName.ToLowerInvariant();
            return lower switch
            {
                var c when c.Contains("building") => MaterialIcons.Business,
                var c when c.Contains("computer") || c.Contains("accessories") => MaterialIcons.Computer,
                var c when c.Contains("furniture") || c.Contains("fitting") => MaterialIcons.Chair,
                var c when c.Contains("library") || c.Contains("book") || c.Contains("material") => MaterialIcons.Book,
                var c when c.Contains("loose") || c.Contains("tool") => MaterialIcons.Build,
                var c when c.Contains("motor") || c.Contains("vehicle") => MaterialIcons.DirectionsCar,
                var c when c.Contains("office") => MaterialIcons.Print,
                var c when c.Contains("plant") || c.Contains("equipment") => MaterialIcons.PrecisionManufacturing,
                var c when c.Contains("road") || c.Contains("curvert") => MaterialIcons.DirectionsRailway,
                var c when c.Contains("software") => MaterialIcons.Code,
                var c when c.Contains("teaching") || c.Contains("aid") || c.Contains("mat") => MaterialIcons.School,
                _ => MaterialIcons.Inventory // Default for unknown categories
            };
        }

        /// <summary>
        /// Refresh the inventory list
        /// </summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (IsRefreshing) return;
            IsRefreshing = true;
            try
            {
                await LoadAssetsAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }
    }

    /// <summary>
    /// ViewModel for individual asset items in the list
    /// </summary>
    public partial class AssetItemViewModel : ObservableObject
    {
        private readonly Action<AssetItemViewModel> _onTapped;

        public AssetItemViewModel(Action<AssetItemViewModel> onTapped)
        {
            _onTapped = onTapped;
        }

        [ObservableProperty]
        private string assetId = string.Empty;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string assetTag = string.Empty;

        [ObservableProperty]
        private string? digitalAssetTag;

        [ObservableProperty]
        private string categoryName = string.Empty;

        [ObservableProperty]
        private MaterialIcons categoryIcon = MaterialIcons.Inventory;

        [ObservableProperty]
        private string locationName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SyncStatusColor))]
        private bool isPendingSync = false;

        [ObservableProperty]
        private DateTime dateModified;

        public string DisplayTag => $"ID: #{AssetTag}";
        public string DisplayLocation => LocationName;
        public Color SyncStatusColor => IsPendingSync ? Color.FromArgb("#FF9800") : Colors.Transparent;

        [RelayCommand]
        private void Tap()
        {
            _onTapped?.Invoke(this);
        }
    }

    public class FilterOption
    {
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
    }
}