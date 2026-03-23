using MobileData.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using NUlid;
using System.Net.Http.Json;
using SharedLocation = Shared.Models.Location;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace MobileApp.Services;

/// <summary>
/// Service for managing locations with API-first operations and offline support
/// </summary>
public class LocationService : ILocationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthService _authService;
    private readonly ILogger<LocationService> _logger;

    public LocationService(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        IAuthService authService,
        ILogger<LocationService> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Get all locations from local database
    /// </summary>
    public async Task<List<SharedLocation>> GetAllLocationsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            return await dbContext.Locations
                .AsNoTracking()
                .OrderBy(l => l.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all locations");
            return new List<SharedLocation>();
        }
    }

    /// <summary>
    /// Get a paginated list of locations from local database
    /// </summary>
    public async Task<List<SharedLocation>> GetLocationsPageAsync(int pageIndex, int pageSize)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            return await dbContext.Locations
                .AsNoTracking()
                .OrderBy(l => l.Name)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting locations page {PageIndex} size {PageSize}", pageIndex, pageSize);
            return new List<SharedLocation>();
        }
    }

    /// <summary>
    /// Get a specific location by ID from local database
    /// </summary>
    public async Task<SharedLocation?> GetLocationByIdAsync(string locationId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            _logger.LogInformation("Searching for location with ID: {LocationId}", locationId);
            
            var location = await dbContext.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LocationId == locationId);

            if (location == null)
            {
                _logger.LogWarning("Location {LocationId} not found in local database", locationId);
                
                // Log all location IDs for debugging
                var allIds = await dbContext.Locations.Select(l => l.LocationId).ToListAsync();
                _logger.LogInformation("Available location IDs: {LocationIds}", string.Join(", ", allIds));
            }
            else
            {
                _logger.LogInformation("Found location: {LocationName} (ID: {LocationId})", location.Name, location.LocationId);
            }

            return location;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting location {LocationId}", locationId);
            return null;
        }
    }

    /// <summary>
    /// Create a new location (API-first, then save locally)
    /// </summary>
    public async Task<(bool Success, string Message, SharedLocation? Location)> CreateLocationAsync(SharedLocation location)
    {
        try
        {
            // Generate ULID if not provided
            if (string.IsNullOrEmpty(location.LocationId))
            {
                location.LocationId = Ulid.NewUlid().ToString();
            }

            location.DateModified = DateTime.UtcNow;

            // Check internet connectivity
            if (!await _authService.IsConnectedToInternet())
            {
                return (false, "No internet connection. Location creation requires online access.", null);
            }

            // Create DTO for API
            var createDto = new LocationCreateDTO
            {
                Name = location.Name,
                Description = location.Description,
                Campus = location.Campus,
                Building = location.Building,
                Room = location.Room,
                Latitude = location.Latitude,
                Longitude = location.Longitude
            };

            // Send to API first
            var httpClient = _httpClientFactory.CreateClient("ApiClient");
            var response = await httpClient.PostAsJsonAsync("api/locations", createDto);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("API error creating location: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                return (false, $"Failed to create location on server: {response.StatusCode}", null);
            }

            // Get the created location from API response
            var createdLocation = await response.Content.ReadFromJsonAsync<LocationReadDTO>();
            if (createdLocation == null)
            {
                return (false, "Failed to parse server response", null);
            }

            // Map API response to SharedLocation model
            var apiLocation = new SharedLocation
            {
                LocationId = createdLocation.LocationId,
                Name = createdLocation.Name,
                Description = createdLocation.Description,
                Campus = createdLocation.Campus,
                Building = createdLocation.Building,
                Room = createdLocation.Room,
                Latitude = createdLocation.Latitude,
                Longitude = createdLocation.Longitude,
                DateModified = DateTime.UtcNow,
                Assets = new List<Shared.Models.Asset>()
            };

            // Save to local database
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

                // Disable change tracking to prevent sync queue entries
                dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
                
                dbContext.Locations.Add(apiLocation);
                var savedCount = await dbContext.SaveChangesAsync();
                
                // Re-enable change tracking
                dbContext.ChangeTracker.AutoDetectChangesEnabled = true;

                _logger.LogInformation("Saved location {LocationId} to local database. Rows affected: {Count}",
                    apiLocation.LocationId, savedCount);
            }

            _logger.LogInformation("Location created successfully: {LocationId} - {Name}", 
                apiLocation.LocationId, apiLocation.Name);

            return (true, "Location created successfully", apiLocation);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error creating location");
            return (false, "Network error. Please check your connection and try again.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location");
            return (false, $"Error creating location: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Update an existing location (API-first, then save locally)
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateLocationAsync(SharedLocation location)
    {
        try
        {
            location.DateModified = DateTime.UtcNow;

            // Check internet connectivity
            if (!await _authService.IsConnectedToInternet())
            {
                return (false, "No internet connection. Location updates require online access.");
            }

            // Create DTO for API
            var updateDto = new LocationUpdateDTO
            {
                LocationId = location.LocationId,
                Name = location.Name,
                Description = location.Description,
                Campus = location.Campus,
                Building = location.Building,
                Room = location.Room,
                Latitude = location.Latitude,
                Longitude = location.Longitude
            };

            // Send to API first
            var httpClient = _httpClientFactory.CreateClient("ApiClient");
            var response = await httpClient.PutAsJsonAsync($"api/locations/{location.LocationId}", updateDto);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("API error updating location: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                return (false, $"Failed to update location on server: {response.StatusCode}");
            }

            // Update local database
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            var existingLocation = await dbContext.Locations
                .FirstOrDefaultAsync(l => l.LocationId == location.LocationId);

            if (existingLocation != null)
            {
                // Update properties
                existingLocation.Name = location.Name;
                existingLocation.Description = location.Description;
                existingLocation.Campus = location.Campus;
                existingLocation.Building = location.Building;
                existingLocation.Room = location.Room;
                existingLocation.Latitude = location.Latitude;
                existingLocation.Longitude = location.Longitude;
                existingLocation.DateModified = location.DateModified;

                // Explicitly mark as modified to ensure changes are saved
                dbContext.Entry(existingLocation).State = EntityState.Modified;
                
                var savedCount = await dbContext.SaveChangesAsync();
                _logger.LogInformation("Updated location in local database. Rows affected: {Count}", savedCount);
            }
            else
            {
                _logger.LogWarning("Location {LocationId} not found in local database for update", location.LocationId);
            }

            _logger.LogInformation("Location updated successfully: {LocationId} - {Name}", 
                location.LocationId, location.Name);

            return (true, "Location updated successfully");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error updating location");
            return (false, "Network error. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location");
            return (false, $"Error updating location: {ex.Message}");
        }
    }

    /// <summary>
    /// Sync locations from API to local database
    /// </summary>
    public async Task<(bool Success, string Message)> SyncLocationsFromApiAsync()
    {
        try
        {
            // Check internet connectivity
            if (!await _authService.IsConnectedToInternet())
            {
                return (false, "No internet connection");
            }

            var httpClient = _httpClientFactory.CreateClient("ApiClient");
            var response = await httpClient.GetAsync("api/locations");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch locations from API: {StatusCode}", response.StatusCode);
                return (false, $"Failed to sync locations: {response.StatusCode}");
            }

            var locations = await response.Content.ReadFromJsonAsync<List<LocationReadDTO>>();
            if (locations == null || !locations.Any())
            {
                return (true, "No locations to sync");
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            // Disable change tracking to prevent sync queue entries
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

            foreach (var locationDto in locations)
            {
                var existingLocation = await dbContext.Locations
                    .FirstOrDefaultAsync(l => l.LocationId == locationDto.LocationId);

                if (existingLocation != null)
                {
                    // Update existing location
                    existingLocation.Name = locationDto.Name;
                    existingLocation.Description = locationDto.Description;
                    existingLocation.Campus = locationDto.Campus;
                    existingLocation.Building = locationDto.Building;
                    existingLocation.Room = locationDto.Room;
                    existingLocation.Latitude = locationDto.Latitude;
                    existingLocation.Longitude = locationDto.Longitude;
                    existingLocation.DateModified = DateTime.UtcNow;
                }
                else
                {
                    // Add new location
                    var newLocation = new SharedLocation
                    {
                        LocationId = locationDto.LocationId,
                        Name = locationDto.Name,
                        Description = locationDto.Description,
                        Campus = locationDto.Campus,
                        Building = locationDto.Building,
                        Room = locationDto.Room,
                        Latitude = locationDto.Latitude,
                        Longitude = locationDto.Longitude,
                        DateModified = DateTime.UtcNow,
                        Assets = new List<Shared.Models.Asset>()
                    };
                    dbContext.Locations.Add(newLocation);
                }
            }

            await dbContext.SaveChangesAsync();

            // Re-enable change tracking
            dbContext.ChangeTracker.AutoDetectChangesEnabled = true;

            _logger.LogInformation("Synced {Count} locations from API", locations.Count);
            return (true, $"Synced {locations.Count} locations successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing locations from API");
            return (false, $"Error syncing locations: {ex.Message}");
        }
    }

    /// <summary>
    /// Get current device location coordinates using MAUI Geolocation
    /// </summary>
    public async Task<(double? Latitude, double? Longitude, string? ErrorMessage)> GetCurrentLocationAsync()
    {
        try
        {
            // Check if location services are available
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                
                if (status != PermissionStatus.Granted)
                {
                    return (null, null, "Location permission denied. Please enable location access in settings.");
                }
            }

            // Get current location with timeout
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            var mauiLocation = await Geolocation.GetLocationAsync(request);

            if (mauiLocation != null)
            {
                _logger.LogInformation("Location acquired: Lat={Latitude}, Lon={Longitude}",
                    mauiLocation.Latitude, mauiLocation.Longitude);
                return (mauiLocation.Latitude, mauiLocation.Longitude, null);
            }

            return (null, null, "Unable to get current location. Please try again.");
        }
        catch (FeatureNotSupportedException)
        {
            _logger.LogWarning("Geolocation not supported on this device");
            return (null, null, "Location services are not supported on this device.");
        }
        catch (PermissionException)
        {
            _logger.LogWarning("Location permission denied");
            return (null, null, "Location permission denied. Please enable location access in settings.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current location");
            return (null, null, $"Error getting location: {ex.Message}");
        }
    }
}