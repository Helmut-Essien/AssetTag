using MobileData.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using Shared.Models;
using System.Net.Http.Json;

namespace MobileApp.Services;

public class SyncService : ISyncService
{
    private readonly LocalDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthService _authService;
    private readonly ILogger<SyncService> _logger;

    public SyncService(
        LocalDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IAuthService authService,
        ILogger<SyncService> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _logger = logger;
    }

    public async Task<(bool Success, string Message)> PushChangesAsync()
    {
        try
        {
            // Check connectivity
            if (!await _authService.IsConnectedToInternet())
            {
                _logger.LogWarning("Push sync skipped: No internet connection");
                return (false, "No internet connection");
            }

            // Get pending sync items
            var pendingItems = await _dbContext.SyncQueue
                .OrderBy(s => s.CreatedAt)
                .ToListAsync();

            if (!pendingItems.Any())
            {
                _logger.LogInformation("Push sync: No changes to sync");
                return (true, "No changes to sync");
            }

            _logger.LogInformation("Push sync: {Count} operations to sync", pendingItems.Count);

            // Prepare request
            var deviceInfo = await GetOrCreateDeviceInfoAsync();
            var request = new SyncPushRequestDTO
            {
                DeviceId = deviceInfo.DeviceId,
                Operations = pendingItems.Select(item => new SyncOperationDTO
                {
                    EntityType = item.EntityType,
                    EntityId = item.EntityId,
                    Operation = item.Operation,
                    JsonData = item.JsonData,
                    CreatedAt = item.CreatedAt
                }).ToList()
            };

            // Send to server
            var httpClient = _httpClientFactory.CreateClient("ApiClient");
            var response = await httpClient.PostAsJsonAsync("api/sync/push", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SyncPushResponseDTO>();
                
                if (result != null)
                {
                    // Remove successfully synced items
                    _dbContext.SyncQueue.RemoveRange(pendingItems);
                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation("Push sync completed: {SuccessCount} synced, {FailureCount} failed", 
                        result.SuccessCount, result.FailureCount);

                    if (result.Errors.Any())
                    {
                        foreach (var error in result.Errors)
                        {
                            _logger.LogError("Sync error for {EntityId}: {Message}", 
                                error.EntityId, error.ErrorMessage);
                        }
                    }

                    return (true, $"Synced {result.SuccessCount} changes");
                }
            }

            _logger.LogError("Push sync failed: {StatusCode}", response.StatusCode);
            return (false, $"Sync failed: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing changes");
            return (false, $"Sync error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> PullChangesAsync()
    {
        try
        {
            if (!await _authService.IsConnectedToInternet())
            {
                _logger.LogWarning("Pull sync skipped: No internet connection");
                return (false, "No internet connection");
            }

            var deviceInfo = await GetOrCreateDeviceInfoAsync();
            var request = new SyncPullRequestDTO
            {
                DeviceId = deviceInfo.DeviceId,
                LastSyncTimestamp = deviceInfo.LastSync
            };

            _logger.LogInformation("Pull sync: Requesting changes since {LastSync}", deviceInfo.LastSync);

            var httpClient = _httpClientFactory.CreateClient("ApiClient");
            var response = await httpClient.PostAsJsonAsync("api/sync/pull", request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Pull sync failed: {StatusCode}", response.StatusCode);
                return (false, $"Pull failed: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<SyncPullResponseDTO>();
            if (result == null)
            {
                _logger.LogError("Pull sync: Invalid response from server");
                return (false, "Invalid response from server");
            }

            var totalChanges = 0;

            // ═══════════════════════════════════════════════════════════
            // STEP 1: Sync Categories FIRST (Assets depend on them)
            // ═══════════════════════════════════════════════════════════
            foreach (var categoryDto in result.Categories)
            {
                var existing = await _dbContext.Categories.FindAsync(categoryDto.CategoryId);
                
                if (existing != null)
                {
                    // UPDATE existing category
                    existing.Name = categoryDto.Name;
                    existing.Description = categoryDto.Description;
                    existing.DepreciationRate = categoryDto.DepreciationRate;
                    existing.DateModified = DateTime.UtcNow;
                    
                    _logger.LogDebug("Updated category: {CategoryName}", categoryDto.Name);
                }
                else
                {
                    // INSERT new category
                    var newCategory = new Category
                    {
                        CategoryId = categoryDto.CategoryId,
                        Name = categoryDto.Name,
                        Description = categoryDto.Description,
                        DepreciationRate = categoryDto.DepreciationRate,
                        DateModified = DateTime.UtcNow
                    };
                    
                    _dbContext.Categories.Add(newCategory);
                    _logger.LogDebug("Added new category: {CategoryName}", categoryDto.Name);
                }
                
                totalChanges++;
            }

            // ═══════════════════════════════════════════════════════════
            // STEP 2: Sync Locations (Assets depend on them)
            // ═══════════════════════════════════════════════════════════
            foreach (var locationDto in result.Locations)
            {
                var existing = await _dbContext.Locations.FindAsync(locationDto.LocationId);
                
                if (existing != null)
                {
                    // UPDATE existing location
                    existing.Name = locationDto.Name;
                    existing.Description = locationDto.Description;
                    existing.Campus = locationDto.Campus;
                    existing.Building = locationDto.Building;
                    existing.Room = locationDto.Room;
                    existing.Latitude = locationDto.Latitude;
                    existing.Longitude = locationDto.Longitude;
                    existing.DateModified = DateTime.UtcNow;
                    
                    _logger.LogDebug("Updated location: {LocationName}", locationDto.Name);
                }
                else
                {
                    // INSERT new location
                    var newLocation = new Shared.Models.Location
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
                        Assets = new List<Asset>()
                    };
                    
                    _dbContext.Locations.Add(newLocation);
                    _logger.LogDebug("Added new location: {LocationName}", locationDto.Name);
                }
                
                totalChanges++;
            }

            // ═══════════════════════════════════════════════════════════
            // STEP 3: Sync Departments (Assets depend on them)
            // ═══════════════════════════════════════════════════════════
            foreach (var departmentDto in result.Departments)
            {
                var existing = await _dbContext.Departments.FindAsync(departmentDto.DepartmentId);
                
                if (existing != null)
                {
                    // UPDATE existing department
                    existing.Name = departmentDto.Name;
                    existing.Description = departmentDto.Description;
                    existing.DateModified = DateTime.UtcNow;
                    
                    _logger.LogDebug("Updated department: {DepartmentName}", departmentDto.Name);
                }
                else
                {
                    // INSERT new department
                    var newDepartment = new Department
                    {
                        DepartmentId = departmentDto.DepartmentId,
                        Name = departmentDto.Name,
                        Description = departmentDto.Description,
                        DateModified = DateTime.UtcNow,
                        Users = new List<ApplicationUser>()
                    };
                    
                    _dbContext.Departments.Add(newDepartment);
                    _logger.LogDebug("Added new department: {DepartmentName}", departmentDto.Name);
                }
                
                totalChanges++;
            }

            // Save reference data changes before processing assets
            await _dbContext.SaveChangesAsync();

            // ═══════════════════════════════════════════════════════════
            // STEP 4: Sync Assets LAST (after all dependencies exist)
            // ═══════════════════════════════════════════════════════════
            foreach (var assetDto in result.Assets)
            {
                // ═══════════════════════════════════════════════════════════
                // Ensure all referenced entities exist before inserting/updating asset
                // ═══════════════════════════════════════════════════════════
                var categoryExists = await _dbContext.Categories.AnyAsync(c => c.CategoryId == assetDto.CategoryId);
                var locationExists = await _dbContext.Locations.AnyAsync(l => l.LocationId == assetDto.LocationId);
                var departmentExists = await _dbContext.Departments.AnyAsync(d => d.DepartmentId == assetDto.DepartmentId);
                
                if (!categoryExists || !locationExists || !departmentExists)
                {
                    _logger.LogWarning(
                        "Skipping asset {AssetId} ({AssetTag}) - missing references: Category={CategoryExists}, Location={LocationExists}, Department={DepartmentExists}",
                        assetDto.AssetId, assetDto.AssetTag, categoryExists, locationExists, departmentExists);
                    continue; // Skip this asset for now, it will sync on next pull when references are available
                }
                
                var existing = await _dbContext.Assets.FindAsync(assetDto.AssetId);
                
                if (existing != null)
                {
                    // UPDATE existing asset
                    existing.AssetTag = assetDto.AssetTag;
                    existing.Name = assetDto.Name;
                    existing.Description = assetDto.Description;
                    existing.CategoryId = assetDto.CategoryId;
                    existing.LocationId = assetDto.LocationId;
                    existing.DepartmentId = assetDto.DepartmentId;
                    existing.PurchaseDate = assetDto.PurchaseDate;
                    existing.PurchasePrice = assetDto.PurchasePrice;
                    existing.CurrentValue = assetDto.CurrentValue;
                    existing.Status = assetDto.Status;
                    existing.AssignedToUserId = assetDto.AssignedToUserId;
                    existing.SerialNumber = assetDto.SerialNumber;
                    existing.DigitalAssetTag = assetDto.DigitalAssetTag;
                    existing.Condition = assetDto.Condition;
                    existing.VendorName = assetDto.VendorName;
                    existing.InvoiceNumber = assetDto.InvoiceNumber;
                    existing.Quantity = assetDto.Quantity;
                    existing.CostPerUnit = assetDto.CostPerUnit;
                    existing.UsefulLifeYears = assetDto.UsefulLifeYears;
                    existing.WarrantyExpiry = assetDto.WarrantyExpiry;
                    existing.DisposalDate = assetDto.DisposalDate;
                    existing.DisposalValue = assetDto.DisposalValue;
                    existing.Remarks = assetDto.Remarks;
                    existing.DateModified = assetDto.DateModified;
                    
                    _logger.LogDebug("Updated asset: {AssetName} ({AssetTag})", 
                        assetDto.Name, assetDto.AssetTag);
                }
                else
                {
                    // INSERT new asset
                    var newAsset = new Asset
                    {
                        AssetId = assetDto.AssetId,
                        AssetTag = assetDto.AssetTag,
                        Name = assetDto.Name,
                        Description = assetDto.Description,
                        CategoryId = assetDto.CategoryId,
                        LocationId = assetDto.LocationId,
                        DepartmentId = assetDto.DepartmentId,
                        PurchaseDate = assetDto.PurchaseDate,
                        PurchasePrice = assetDto.PurchasePrice,
                        CurrentValue = assetDto.CurrentValue,
                        Status = assetDto.Status,
                        AssignedToUserId = assetDto.AssignedToUserId,
                        CreatedAt = assetDto.CreatedAt,
                        DateModified = assetDto.DateModified,
                        SerialNumber = assetDto.SerialNumber,
                        DigitalAssetTag = assetDto.DigitalAssetTag,
                        Condition = assetDto.Condition,
                        VendorName = assetDto.VendorName,
                        InvoiceNumber = assetDto.InvoiceNumber,
                        Quantity = assetDto.Quantity,
                        CostPerUnit = assetDto.CostPerUnit,
                        UsefulLifeYears = assetDto.UsefulLifeYears,
                        WarrantyExpiry = assetDto.WarrantyExpiry,
                        DisposalDate = assetDto.DisposalDate,
                        DisposalValue = assetDto.DisposalValue,
                        Remarks = assetDto.Remarks
                    };
                    
                    _dbContext.Assets.Add(newAsset);
                    _logger.LogDebug("Added new asset: {AssetName} ({AssetTag})", 
                        assetDto.Name, assetDto.AssetTag);
                }
                
                totalChanges++;
            }

            // Save all asset changes
            await _dbContext.SaveChangesAsync();

            // ═══════════════════════════════════════════════════════════
            // STEP 5: Update last sync timestamp ONLY after successful sync
            // ═══════════════════════════════════════════════════════════
            deviceInfo.LastSync = result.ServerTimestamp;
            await _dbContext.SaveChangesAsync();

            var message = $"Synced {totalChanges} changes: " +
                         $"{result.Categories.Count} categories, " +
                         $"{result.Locations.Count} locations, " +
                         $"{result.Departments.Count} departments, " +
                         $"{result.Assets.Count} assets";

            _logger.LogInformation("Pull sync completed successfully: {Message}", message);
            
            return (true, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pulling changes");
            return (false, $"Pull error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> FullSyncAsync()
    {
        _logger.LogInformation("Starting full sync (push + pull)");

        // Push first
        var (pushSuccess, pushMessage) = await PushChangesAsync();
        if (!pushSuccess)
        {
            _logger.LogWarning("Full sync: Push failed - {Message}", pushMessage);
            return (false, $"Push failed: {pushMessage}");
        }

        // Then pull
        var (pullSuccess, pullMessage) = await PullChangesAsync();
        if (!pullSuccess)
        {
            _logger.LogWarning("Full sync: Pull failed - {Message}", pullMessage);
            return (false, $"Pull failed: {pullMessage}");
        }

        _logger.LogInformation("Full sync completed successfully");
        return (true, $"Sync complete. {pushMessage}. {pullMessage}");
    }

    public async Task<int> GetPendingSyncCountAsync()
    {
        return await _dbContext.SyncQueue.CountAsync();
    }

    private async Task<MobileData.Data.DeviceInfo> GetOrCreateDeviceInfoAsync()
    {
        var deviceInfo = await _dbContext.DeviceInfo.FirstOrDefaultAsync();
        if (deviceInfo == null)
        {
            // For first-time install, use a very old date (year 1900) to fetch ALL data from server
            // This ensures complete initial sync on first app launch
            var initialSyncDate = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            
            deviceInfo = new MobileData.Data.DeviceInfo
            {
                DeviceId = Guid.NewGuid().ToString(),
                LastSync = initialSyncDate,
                SyncToken = string.Empty
            };
            _dbContext.DeviceInfo.Add(deviceInfo);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Created new device info with ID: {DeviceId}, LastSync: {LastSync} (initial full sync)",
                deviceInfo.DeviceId, deviceInfo.LastSync);
        }
        return deviceInfo;
    }

    /// <summary>
    /// Reset sync state to force a full re-sync from server.
    /// Use this when local database is corrupted or out of sync.
    /// </summary>
    public async Task ResetSyncStateAsync()
    {
        _logger.LogWarning("Resetting sync state - will perform full re-sync on next pull");
        
        var deviceInfo = await _dbContext.DeviceInfo.FirstOrDefaultAsync();
        if (deviceInfo != null)
        {
            // Reset to 1900 to fetch all data
            deviceInfo.LastSync = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Sync state reset. LastSync set to {LastSync}", deviceInfo.LastSync);
        }
    }
}