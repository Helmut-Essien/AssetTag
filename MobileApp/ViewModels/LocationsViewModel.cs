using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Services;
using MauiIcons.Material;
using System.Collections.ObjectModel;
using SharedLocation = Shared.Models.Location;

namespace MobileApp.ViewModels;

/// <summary>
/// ViewModel for the Locations screen with API-first operations
/// </summary>
public partial class LocationsViewModel : BaseViewModel
{
    private readonly ILocationService _locationService;
    private readonly IAuthService _authService;

        [ObservableProperty]
        private ObservableCollection<LocationItemViewModel> locations = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool hasLocations = false;

    [ObservableProperty]
    private bool showEmptyState = false;

    [ObservableProperty]
    private string emptyStateMessage = "No locations found. Tap '+' to add one!";

    [ObservableProperty]
    private string currentSortOption = "Name (A-Z)";

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private bool isInitialLoad = true;

        [ObservableProperty]
        private bool isLoadingMore = false;

    [ObservableProperty]
    private bool isCapturingLocation = false;

    private bool _isLoading = false;
    private bool _reloadRequested = false;
    private bool _suppressSearchReload = false;
    private int _pageIndex = 0;
    private const int PageSize = 50;
    private bool _hasMoreItems = true;
    private CancellationTokenSource? _searchCts;

    public LocationsViewModel(
        ILocationService locationService,
        IAuthService authService)
    {
        _locationService = locationService;
        _authService = authService;
        Title = "Locations";
        
        // Start with IsBusy = true so skeleton shows immediately
        IsBusy = true;
    }

    /// <summary>
    /// Load locations from the database
    /// </summary>
    [RelayCommand]
    public async Task LoadLocationsAsync()
    {
        if (_isLoading)
        {
            _reloadRequested = true;
            return;
        }

        try
        {
            _isLoading = true;
            
            if (IsInitialLoad)
            {
                IsBusy = true;
                UpdateVisibilityState();
            }

            // Reset paging state
            _pageIndex = 0;
            _hasMoreItems = true;

            await LoadNextPageAsync(reset: true);

            // Token validation in background
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
            System.Diagnostics.Debug.WriteLine($"Error loading locations: {ex.Message}");
            await Shell.Current.DisplayAlert("Error", "Failed to load locations. Please try again.", "OK");
        }
        finally
        {
            _isLoading = false;
            IsBusy = false;
            IsInitialLoad = false;
            UpdateVisibilityState();

            if (_reloadRequested)
            {
                _reloadRequested = false;
                await LoadLocationsAsync();
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
            var page = await _locationService.GetLocationsPageAsync(
                _pageIndex,
                PageSize,
                searchText: SearchText,
                sortOption: CurrentSortOption);

            if (page == null || page.Count == 0)
            {
                _hasMoreItems = false;
                if (reset)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Locations = new ObservableCollection<LocationItemViewModel>();
                        UpdateVisibilityState();
                    });
                }
                return;
            }

            var newItems = await Task.Run(() =>
            {
                var list = new List<LocationItemViewModel>(page.Count);
                foreach (var location in page)
                {
                    list.Add(new LocationItemViewModel(OnLocationTapped)
                    {
                        LocationId = location.LocationId,
                        Name = location.Name,
                        Description = location.Description,
                        Campus = location.Campus,
                        Building = location.Building,
                        Room = location.Room,
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        DateModified = location.DateModified
                    });
                }
                return list;
            });

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (reset)
                {
                    Locations = new ObservableCollection<LocationItemViewModel>(newItems);
                }
                else
                {
                    foreach (var item in newItems)
                        Locations.Add(item);
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
            System.Diagnostics.Debug.WriteLine($"Error loading locations page: {ex.Message}");
        }
    }

        private void UpdateVisibilityState()
        {
            HasLocations = Locations.Count > 0;
            ShowEmptyState = !IsBusy && Locations.Count == 0;
            EmptyStateMessage = !string.IsNullOrEmpty(SearchText)
                ? "No locations match your search"
            : "No locations found. Tap '+' to add one!";
        }

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

        await LoadLocationsAsync();
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
            "Campus (A-Z)",
            "Date Modified (Newest)",
            "Date Modified (Oldest)");

        if (action != null && action != "Cancel")
        {
            CurrentSortOption = action;
            await ReloadFromDatabaseAsync(debounce: false);
        }
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
        _ = ReloadFromDatabaseAsync(debounce: false);
    }

        [RelayCommand]
        private async Task AddLocationAsync()
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(Views.AddLocationPage));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to navigate: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// View/Edit location details
    /// </summary>
    [RelayCommand]
    private async Task ViewLocationDetailsAsync(LocationItemViewModel location)
    {
        if (location == null) return;

        try
        {
            var action = await Shell.Current.DisplayActionSheet(
                $"{location.Name}",
                "Cancel",
                null,
                "View Details",
                "Edit Location");

            if (action == "View Details")
            {
                await ShowLocationDetailsAsync(location);
            }
            else if (action == "Edit Location")
            {
                await EditLocationAsync(location);
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    /// <summary>
    /// Callback for when a location item is tapped (optimized for direct binding)
    /// </summary>
    private void OnLocationTapped(LocationItemViewModel location)
    {
        // Execute on UI thread
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await ViewLocationDetailsAsync(location);
        });
    }

    private async Task ShowLocationDetailsAsync(LocationItemViewModel location)
    {
        var details = $"Campus: {location.Campus}\n";
        
        if (!string.IsNullOrEmpty(location.Building))
            details += $"Building: {location.Building}\n";
        
        if (!string.IsNullOrEmpty(location.Room))
            details += $"Room: {location.Room}\n";
        
        if (!string.IsNullOrEmpty(location.Description))
            details += $"\nDescription: {location.Description}\n";
        
        if (location.Latitude.HasValue && location.Longitude.HasValue)
            details += $"\nCoordinates:\nLat: {location.Latitude:F6}\nLon: {location.Longitude:F6}";

        await Shell.Current.DisplayAlert(location.Name, details, "OK");
    }

    private async Task EditLocationAsync(LocationItemViewModel locationItem)
    {
        try
        {
            await Shell.Current.GoToAsync($"{nameof(Views.EditLocationPage)}?locationId={locationItem.LocationId}");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to navigate: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Sync locations from API
    /// </summary>
    [RelayCommand]
    private async Task SyncLocationsAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;

            var (success, message) = await _locationService.SyncLocationsFromApiAsync();
            
            await Shell.Current.DisplayAlert(
                success ? "Sync Complete" : "Sync Error",
                message,
                "OK");

            if (success)
            {
                await LoadLocationsAsync();
            }
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
    /// Refresh the locations list
    /// </summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (IsRefreshing) return;
            IsRefreshing = true;
            try
            {
                await LoadLocationsAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }
}

/// <summary>
/// ViewModel for individual location items in the list
/// </summary>
public partial class LocationItemViewModel : ObservableObject
{
    private readonly Action<LocationItemViewModel> _onTapped;

    public LocationItemViewModel(Action<LocationItemViewModel> onTapped)
    {
        _onTapped = onTapped;
    }

    [ObservableProperty]
    private string locationId = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private string campus = string.Empty;

    [ObservableProperty]
    private string? building;

    [ObservableProperty]
    private string? room;

    [ObservableProperty]
    private double? latitude;

    [ObservableProperty]
    private double? longitude;

    [ObservableProperty]
    private DateTime dateModified;

    public string DisplayAddress
    {
        get
        {
            var parts = new List<string>();
            
            if (!string.IsNullOrEmpty(Building))
                parts.Add(Building);
            
            if (!string.IsNullOrEmpty(Room))
                parts.Add($"Room {Room}");
            
            parts.Add(Campus);
            
            return string.Join(" • ", parts);
        }
    }

    public MaterialIcons LocationIcon => MaterialIcons.LocationOn;
    
    public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;

    [RelayCommand]
    private void Tap()
    {
        _onTapped?.Invoke(this);
    }
}