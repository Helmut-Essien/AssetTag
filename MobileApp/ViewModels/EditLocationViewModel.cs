using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Services;
using SharedLocation = Shared.Models.Location;

namespace MobileApp.ViewModels;

/// <summary>
/// ViewModel for editing an existing location
/// </summary>
public partial class EditLocationViewModel : BaseViewModel
{
    private readonly ILocationService _locationService;
    private readonly IAuthService _authService;
    private string _locationId = string.Empty;

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
    private bool hasCoordinates = false;

    [ObservableProperty]
    private bool isCapturingLocation = false;

    [ObservableProperty]
    private string coordinatesDisplay = "No coordinates captured";

    [ObservableProperty]
    private bool isLoading = true;

    public EditLocationViewModel(
        ILocationService locationService,
        IAuthService authService)
    {
        _locationService = locationService;
        _authService = authService;
        Title = "Edit Location";
    }

    /// <summary>
    /// Initialize with location ID
    /// </summary>
    public async Task InitializeAsync(string locationId)
    {
        _locationId = locationId;
        await LoadLocationAsync();
    }

    /// <summary>
    /// Load location data
    /// </summary>
    private async Task LoadLocationAsync()
    {
        try
        {
            IsLoading = true;

            var location = await _locationService.GetLocationByIdAsync(_locationId);
            if (location == null)
            {
                await Shell.Current.DisplayAlert("Error", "Location not found", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Populate fields
            Name = location.Name;
            Description = location.Description;
            Campus = location.Campus;
            Building = location.Building;
            Room = location.Room;
            Latitude = location.Latitude;
            Longitude = location.Longitude;

            if (Latitude.HasValue && Longitude.HasValue)
            {
                HasCoordinates = true;
                CoordinatesDisplay = $"Lat: {Latitude.Value:F6}, Lon: {Longitude.Value:F6}";
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to load location: {ex.Message}", "OK");
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Capture current GPS coordinates
    /// </summary>
    [RelayCommand]
    private async Task CaptureLocationAsync()
    {
        if (IsCapturingLocation) return;

        try
        {
            IsCapturingLocation = true;

            var (lat, lon, error) = await _locationService.GetCurrentLocationAsync();

            if (error != null)
            {
                await Shell.Current.DisplayAlert("Location Error", error, "OK");
                return;
            }

            if (lat.HasValue && lon.HasValue)
            {
                Latitude = lat.Value;
                Longitude = lon.Value;
                HasCoordinates = true;
                CoordinatesDisplay = $"Lat: {lat.Value:F6}, Lon: {lon.Value:F6}";
                
                await Shell.Current.DisplayAlert(
                    "Success",
                    "GPS coordinates captured successfully!",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to capture location: {ex.Message}", "OK");
        }
        finally
        {
            IsCapturingLocation = false;
        }
    }

    /// <summary>
    /// Clear captured coordinates
    /// </summary>
    [RelayCommand]
    private void ClearCoordinates()
    {
        Latitude = null;
        Longitude = null;
        HasCoordinates = false;
        CoordinatesDisplay = "No coordinates captured";
    }

    /// <summary>
    /// Save the updated location
    /// </summary>
    [RelayCommand]
    private async Task SaveLocationAsync()
    {
        if (IsBusy) return;

        // Validate required fields
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlert("Validation Error", "Location name is required", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(Campus))
        {
            await Shell.Current.DisplayAlert("Validation Error", "Campus is required", "OK");
            return;
        }

        // Check internet connectivity
        if (!await _authService.IsConnectedToInternet())
        {
            await Shell.Current.DisplayAlert(
                "No Connection",
                "Updating locations requires an internet connection.",
                "OK");
            return;
        }

        try
        {
            IsBusy = true;

            var updatedLocation = new SharedLocation
            {
                LocationId = _locationId,
                Name = Name.Trim(),
                Campus = Campus.Trim(),
                Building = string.IsNullOrWhiteSpace(Building) ? null : Building.Trim(),
                Room = string.IsNullOrWhiteSpace(Room) ? null : Room.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                Latitude = Latitude,
                Longitude = Longitude,
                Assets = new List<Shared.Models.Asset>()
            };

            var (success, message) = await _locationService.UpdateLocationAsync(updatedLocation);

            if (success)
            {
                await Shell.Current.DisplayAlert("Success", message, "OK");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", message, "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to update location: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Cancel and go back
    /// </summary>
    [RelayCommand]
    private async Task CancelAsync()
    {
        var confirm = await Shell.Current.DisplayAlert(
            "Confirm",
            "Discard changes and go back?",
            "Yes",
            "No");

        if (confirm)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}