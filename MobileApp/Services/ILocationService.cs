using SharedLocation = Shared.Models.Location;

namespace MobileApp.Services;

/// <summary>
/// Service interface for managing locations with API-first operations
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Get all locations from local database
    /// </summary>
    Task<List<SharedLocation>> GetAllLocationsAsync();

    /// <summary>
    /// Get a paginated list of locations from local database with optional search/sort in SQL
    /// </summary>
    Task<List<SharedLocation>> GetLocationsPageAsync(
        int pageIndex,
        int pageSize,
        string? searchText = null,
        string sortOption = "Name (A-Z)");

    /// <summary>
    /// Get a specific location by ID from local database
    /// </summary>
    Task<SharedLocation?> GetLocationByIdAsync(string locationId);

    /// <summary>
    /// Create a new location (API-first, then save locally)
    /// </summary>
    Task<(bool Success, string Message, SharedLocation? Location)> CreateLocationAsync(SharedLocation location);

    /// <summary>
    /// Update an existing location (API-first, then save locally)
    /// </summary>
    Task<(bool Success, string Message)> UpdateLocationAsync(SharedLocation location);

    /// <summary>
    /// Sync locations from API to local database
    /// </summary>
    Task<(bool Success, string Message)> SyncLocationsFromApiAsync();

    /// <summary>
    /// Get current device location coordinates
    /// </summary>
    Task<(double? Latitude, double? Longitude, string? ErrorMessage)> GetCurrentLocationAsync();

    /// <summary>
    /// Distinct location names for filter UI, ordered alphabetically.
    /// </summary>
    Task<List<string>> GetLocationNamesAsync();
}