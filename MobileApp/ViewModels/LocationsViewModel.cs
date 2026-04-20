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
    private IReadOnlyList<LocationItemViewModel> filteredLocations = new List<LocationItemViewModel>();

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
    private bool isInitialLoad = true;

    [ObservableProperty]
    private bool isLoadingMore = false;

    [ObservableProperty]
    private bool isCapturingLocation = false;

    private bool _isLoading = false;
    private int _pageIndex = 0;
    private const int PageSize = 50;
    private bool _hasMoreItems = true;

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
        if (_isLoading) return;

        try
        {
            _isLoading = true;
            
            if (IsInitialLoad)
            {
                IsBusy = true;
            }

            // Reset paging state
            _pageIndex = 0;
            _hasMoreItems = true;
            Locations.Clear();

            // Load first page
            await LoadNextPageAsync();

            // Update empty/has locations state
            MainThread.BeginInvokeOnMainThread(() =>
            {
                HasLocations = Locations.Count > 0;

                if (ShowEmptyState && !string.IsNullOrEmpty(SearchText))
                {
                    EmptyStateMessage = "No locations match your search";
                }
                else
                {
                    EmptyStateMessage = "No locations found. Tap '+' to add one!";
                }
            });

            // Token validation in background
            _ = Task.Run(async () =>
            {
                var tokenValid = await TryValidateTokenSilentAsync(_authService);
                if (!tokenValid)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            await Shell.Current.GoToAsync("/LoginPage");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Navigation to login failed: {ex.Message}");
                        }
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
        }
    }

    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (IsLoadingMore || !_hasMoreItems) return;
        
        try
        {
            IsLoadingMore = true;
            await LoadNextPageAsync();
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private async Task LoadNextPageAsync()
    {
        try
        {
            var page = await _locationService.GetLocationsPageAsync(_pageIndex, PageSize);
            if (page == null || page.Count == 0)
            {
                _hasMoreItems = false;
                return;
            }

            // Map on background thread
            var newItems = await Task.Run(() =>
            {
                var list = new List<LocationItemViewModel>(page.Count);
                foreach (var location in page)
                {
                    var item = new LocationItemViewModel(OnLocationTapped)
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
                    };
                    list.Add(item);
                }
                return list;
            });

            // Append to collection on main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var item in newItems)
                    Locations.Add(item);

                ApplyFilters();
            });

            // Advance page index and check for more
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

    /// <summary>
    /// Apply search and filter logic
    /// </summary>
    private void ApplyFilters()
    {
        var filtered = Locations.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLower();
            filtered = filtered.Where(l =>
                l.Name.ToLower().Contains(searchLower) ||
                (l.Campus?.ToLower().Contains(searchLower) ?? false) ||
                (l.Building?.ToLower().Contains(searchLower) ?? false) ||
                (l.Room?.ToLower().Contains(searchLower) ?? false));
        }

        // Apply sorting
        filtered = CurrentSortOption switch
        {
            "Name (A-Z)" => filtered.OrderBy(l => l.Name),
            "Name (Z-A)" => filtered.OrderByDescending(l => l.Name),
            "Campus (A-Z)" => filtered.OrderBy(l => l.Campus).ThenBy(l => l.Name),
            "Date Modified (Newest)" => filtered.OrderByDescending(l => l.DateModified),
            "Date Modified (Oldest)" => filtered.OrderBy(l => l.DateModified),
            _ => filtered.OrderBy(l => l.Name)
        };

        var result = filtered.ToList();
        FilteredLocations = result;

        ShowEmptyState = FilteredLocations.Count == 0;
    }

    /// <summary>
    /// Handle search text changes
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
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
            "Campus (A-Z)",
            "Date Modified (Newest)",
            "Date Modified (Oldest)");

        if (action != null && action != "Cancel")
        {
            CurrentSortOption = action;
            ApplyFilters();
        }
    }

    /// <summary>
    /// Clear all filters
    /// </summary>
    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        ApplyFilters();
    }

    /// <summary>
    /// Go back
    /// </summary>
    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>
    /// Navigate to add location page
    /// </summary>
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
        await LoadLocationsAsync();
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