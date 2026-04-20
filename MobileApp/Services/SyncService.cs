using MobileData.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using Shared.Models;
using System.Net.Http.Json;
using System.Threading.Channels;

namespace MobileApp.Services;

public class SyncService : ISyncService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthService _authService;
    private readonly ILogger<SyncService> _logger;
    
    // FIX #4: Separate semaphores for each sync operation to prevent race conditions
    // These ensure only one sync operation of each type runs at a time
    private readonly SemaphoreSlim _pushSemaphore = new(1, 1);
    private readonly SemaphoreSlim _pullSemaphore = new(1, 1);
    private readonly SemaphoreSlim _fullSyncSemaphore = new(1, 1);
    
    // Channel-based queue to serialize background sync requests
    private readonly Channel<SyncWorkItem> _syncQueue = Channel.CreateUnbounded<SyncWorkItem>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly Task _queueProcessorTask;

    // FIX #2: Maximum retry attempts before marking sync item as permanently failed
    private const int MAX_RETRY_COUNT = 5;

    private record SyncWorkItem(SyncRequestType Type, TaskCompletionSource<(bool Success, string Message)> Tcs);

    private enum SyncRequestType { Push, Full }

    // FIX #6: Sync progress event system for real-time UI updates
    public event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;

    /// <summary>
    /// Represents the current state and progress of a sync operation
    /// </summary>
    public class SyncProgressEventArgs : EventArgs
    {
        public SyncPhase Phase { get; set; }
        public int CurrentItem { get; set; }
        public int TotalItems { get; set; }
        public string Message { get; set; } = string.Empty;
        public double ProgressPercentage => TotalItems > 0 ? (double)CurrentItem / TotalItems * 100 : 0;
    }

    /// <summary>
    /// Phases of the sync operation for progress tracking
    /// </summary>
    public enum SyncPhase
    {
        Starting,
        PushingChanges,
        PullingCategories,
        PullingLocations,
        PullingDepartments,
        PullingAssets,
        Finalizing,
        Completed,
        Failed
    }

    /// <summary>
    /// Raises sync progress event on main thread for UI updates
    /// </summary>
    private void ReportProgress(SyncPhase phase, int current, int total, string message)
    {
        var args = new SyncProgressEventArgs
        {
            Phase = phase,
            CurrentItem = current,
            TotalItems = total,
            Message = message
        };

        // FIX #6: Invoke on main thread for UI safety
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SyncProgressChanged?.Invoke(this, args);
        });

        _logger.LogDebug("Sync progress: {Phase} - {Current}/{Total} - {Message}",
            phase, current, total, message);
    }

    public SyncService(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        IAuthService authService,
        ILogger<SyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _logger = logger;
        // Start background processor for sync queue
        _queueProcessorTask = Task.Run(ProcessQueueAsync);
    }

    public async Task<(bool Success, string Message)> PushChangesAsync()
    {
        // FIX #4: Protect push operations with semaphore to prevent concurrent execution
        // Try to acquire immediately - if already running, skip to avoid queuing
        var acquired = await _pushSemaphore.WaitAsync(0);
        if (!acquired)
        {
            _logger.LogWarning("Push sync already in progress - skipping concurrent request");
            return (false, "Push sync already in progress");
        }

        try
        {
            // FIX #6: Report starting phase
            ReportProgress(SyncPhase.Starting, 0, 0, "Checking connectivity...");

            // Check connectivity
            if (!await _authService.IsConnectedToInternet())
            {
                _logger.LogWarning("Push sync skipped: No internet connection");
                ReportProgress(SyncPhase.Failed, 0, 0, "No internet connection");
                return (false, "No internet connection");
            }

            // Resolve a scoped DbContext for this operation to avoid capturing a long-lived context
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            // Get pending sync items
            var pendingItems = await dbContext.SyncQueue
                .OrderBy(s => s.CreatedAt)
                .ToListAsync();

            if (!pendingItems.Any())
            {
                _logger.LogInformation("Push sync: No changes to sync");
                ReportProgress(SyncPhase.Completed, 0, 0, "No changes to sync");
                return (true, "No changes to sync");
            }

            // FIX #6: Report push phase with total count
            _logger.LogInformation("Push sync: {Count} operations to sync", pendingItems.Count);
            ReportProgress(SyncPhase.PushingChanges, 0, pendingItems.Count,
                $"Pushing {pendingItems.Count} changes to server...");

            // Prepare request
            var deviceInfo = await GetOrCreateDeviceInfoAsync();
            var request = new SyncPushRequestDTO
            {
                DeviceId = deviceInfo.DeviceId,
                Operations = pendingItems.Select(item => new SyncOperationDTO
                {
                    QueueItemId = item.Id, // Include queue item ID for tracking
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
                    // BUG FIX #1: Only remove items that were successfully synced
                    // Get the items that succeeded based on the IDs returned from server
                    var successfulItems = pendingItems
                        .Where(item => result.SuccessfulOperationIds.Contains(item.Id))
                        .ToList();
                    
                    dbContext.SyncQueue.RemoveRange(successfulItems);
                    
                    // FIX #2: Increment retry count for failed items and remove those exceeding max retries
                    var failedItems = pendingItems
                        .Where(item => !result.SuccessfulOperationIds.Contains(item.Id))
                        .ToList();
                    
                    var itemsToRemove = new List<SyncQueueItem>();
                    var itemsToRetry = new List<SyncQueueItem>();
                    
                    foreach (var failedItem in failedItems)
                    {
                        failedItem.RetryCount++;
                        
                        if (failedItem.RetryCount >= MAX_RETRY_COUNT)
                        {
                            // FIX #2: Remove permanently failed items to prevent infinite retry loops
                            itemsToRemove.Add(failedItem);
                            _logger.LogError(
                                "Max retry count ({MaxRetries}) exceeded for {EntityType} {EntityId}. " +
                                "Removing from sync queue. Manual intervention required. " +
                                "Operation: {Operation}, Data: {JsonData}",
                                MAX_RETRY_COUNT, failedItem.EntityType, failedItem.EntityId,
                                failedItem.Operation, failedItem.JsonData);
                        }
                        else
                        {
                            itemsToRetry.Add(failedItem);
                            _logger.LogWarning("Sync failed for {EntityType} {EntityId}, retry count: {RetryCount}/{MaxRetries}",
                                failedItem.EntityType, failedItem.EntityId, failedItem.RetryCount, MAX_RETRY_COUNT);
                        }
                    }
                    
                    // FIX #2: Remove items that exceeded max retries
                    if (itemsToRemove.Any())
                    {
                        dbContext.SyncQueue.RemoveRange(itemsToRemove);
                        _logger.LogWarning("Removed {Count} permanently failed items from sync queue", itemsToRemove.Count);
                    }
                    
                    await dbContext.SaveChangesAsync();

                    var message = $"Synced {result.SuccessCount} changes, {itemsToRetry.Count} will retry, {itemsToRemove.Count} permanently failed";
                    _logger.LogInformation("Push sync completed: {Message}", message);

                    // FIX #6: Report completion
                    ReportProgress(SyncPhase.Completed, result.SuccessCount, pendingItems.Count, message);

                    if (result.Errors.Any())
                    {
                        foreach (var error in result.Errors)
                        {
                            _logger.LogError("Sync error for {EntityId}: {Message}",
                                error.EntityId, error.ErrorMessage);
                        }
                    }

                    return (true, message);
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
        finally
        {
            // FIX #4: Always release semaphore, even on error
            _pushSemaphore.Release();
        }
    }

    public async Task<(bool Success, string Message)> EnqueuePushAsync()
    {
        var tcs = new TaskCompletionSource<(bool, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new SyncWorkItem(SyncRequestType.Push, tcs);
        await _syncQueue.Writer.WriteAsync(item);
        return await tcs.Task;
    }

    public async Task<(bool Success, string Message)> PullChangesAsync()
    {
        // FIX #4: Protect pull operations with semaphore to prevent concurrent execution
        var acquired = await _pullSemaphore.WaitAsync(0);
        if (!acquired)
        {
            _logger.LogWarning("Pull sync already in progress - skipping concurrent request");
            return (false, "Pull sync already in progress");
        }

        try
        {
            if (!await _authService.IsConnectedToInternet())
            {
                _logger.LogWarning("Pull sync skipped: No internet connection");
                return (false, "No internet connection");
            }

            // Resolve a scoped DbContext for this pull operation
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

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

            // ═══════════════════════════════════════════════════════════
            // BUG FIX #5: Disable change tracking during pull sync
            // This prevents creating SyncQueue entries for data pulled FROM server
            // CRITICAL: Must remain disabled until ALL SaveChangesAsync() calls complete
            // ═══════════════════════════════════════════════════════════
            try
            {
                dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
                
                var totalChanges = 0;
                var skippedAssetIds = new List<string>(); // BUG FIX #2: Track skipped assets

                // ═══════════════════════════════════════════════════════════
                // STEP 1: Sync Categories FIRST (Assets depend on them)
                // ═══════════════════════════════════════════════════════════
                // FIX #6: Report categories phase
                ReportProgress(SyncPhase.PullingCategories, 0, result.Categories.Count,
                    $"Syncing {result.Categories.Count} categories...");
                
                var categoryIndex = 0;
                foreach (var categoryDto in result.Categories)
                {
                    var existing = await dbContext.Categories.FindAsync(categoryDto.CategoryId);
                    
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
                        
                        dbContext.Categories.Add(newCategory);
                        _logger.LogDebug("Added new category: {CategoryName}", categoryDto.Name);
                    }
                    
                    totalChanges++;
                    categoryIndex++;
                }

                // ═══════════════════════════════════════════════════════════
                // STEP 2: Sync Locations (Assets depend on them)
                // ═══════════════════════════════════════════════════════════
                // FIX #6: Report locations phase
                ReportProgress(SyncPhase.PullingLocations, 0, result.Locations.Count,
                    $"Syncing {result.Locations.Count} locations...");
                
                var locationIndex = 0;
                foreach (var locationDto in result.Locations)
                {
                    var existing = await dbContext.Locations.FindAsync(locationDto.LocationId);
                    
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
                        
                        dbContext.Locations.Add(newLocation);
                        _logger.LogDebug("Added new location: {LocationName}", locationDto.Name);
                    }
                    
                    totalChanges++;
                    locationIndex++;
                }

                // ═══════════════════════════════════════════════════════════
                // STEP 3: Sync Departments (Assets depend on them)
                // ═══════════════════════════════════════════════════════════
                // FIX #6: Report departments phase
                ReportProgress(SyncPhase.PullingDepartments, 0, result.Departments.Count,
                    $"Syncing {result.Departments.Count} departments...");
                
                var departmentIndex = 0;
                foreach (var departmentDto in result.Departments)
                {
                    var existing = await dbContext.Departments.FindAsync(departmentDto.DepartmentId);
                    
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
                        
                        dbContext.Departments.Add(newDepartment);
                        _logger.LogDebug("Added new department: {DepartmentName}", departmentDto.Name);
                    }
                    
                    totalChanges++;
                    departmentIndex++;
                }

                // Save reference data changes before processing assets
                // IMPORTANT: Change tracking is DISABLED - no SyncQueue entries will be created
                await dbContext.SaveChangesAsync();

                // ═══════════════════════════════════════════════════════════
                // STEP 4: Sync Assets LAST (after all dependencies exist)
                // Process assets in batches to avoid long-running transactions and memory spikes
                // ═══════════════════════════════════════════════════════════
                // FIX #6: Report assets phase
                ReportProgress(SyncPhase.PullingAssets, 0, result.Assets.Count,
                    $"Syncing {result.Assets.Count} assets...");
                
                const int ASSET_BATCH_SIZE = 200;
                var assets = result.Assets;
                var assetIndex = 0;

                for (int offset = 0; offset < assets.Count; offset += ASSET_BATCH_SIZE)
                {
                    var batch = assets.Skip(offset).Take(ASSET_BATCH_SIZE).ToList();

                    foreach (var assetDto in batch)
                    {
                        var categoryExists = await dbContext.Categories.AnyAsync(c => c.CategoryId == assetDto.CategoryId);
                        var locationExists = await dbContext.Locations.AnyAsync(l => l.LocationId == assetDto.LocationId);
                        var departmentExists = await dbContext.Departments.AnyAsync(d => d.DepartmentId == assetDto.DepartmentId);

                        if (!categoryExists || !locationExists || !departmentExists)
                        {
                            _logger.LogWarning(
                                "Skipping asset {AssetId} ({AssetTag}) - missing references: Category={CategoryExists}, Location={LocationExists}, Department={DepartmentExists}",
                                assetDto.AssetId, assetDto.AssetTag, categoryExists, locationExists, departmentExists);

                            skippedAssetIds.Add(assetDto.AssetId);
                            continue;
                        }

                        var existing = await dbContext.Assets.FindAsync(assetDto.AssetId);

                        if (existing != null)
                        {
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

                            _logger.LogDebug("Updated asset: {AssetName} ({AssetTag})", assetDto.Name, assetDto.AssetTag);
                        }
                        else
                        {
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

                            dbContext.Assets.Add(newAsset);
                            _logger.LogDebug("Added new asset: {AssetName} ({AssetTag})", assetDto.Name, assetDto.AssetTag);
                        }

                        totalChanges++;
                        assetIndex++;
                        
                        // FIX #6: Report progress every 10 assets to avoid too many UI updates
                        if (assetIndex % 10 == 0 || assetIndex == assets.Count)
                        {
                            ReportProgress(SyncPhase.PullingAssets, assetIndex, assets.Count,
                                $"Syncing assets: {assetIndex}/{assets.Count}");
                        }
                    }

                    // Save each batch to keep transactions bounded and avoid big memory/GC spikes
                    await dbContext.SaveChangesAsync();
                }

                // ═══════════════════════════════════════════════════════════
                // STEP 5: Process deleted items (FIX #5)
                // Remove entities that were deleted on the server
                // ═══════════════════════════════════════════════════════════
                ReportProgress(SyncPhase.Finalizing, 0, result.DeletedItems.Count,
                    $"Processing {result.DeletedItems.Count} deleted items...");
                
                var deletedCount = 0;
                foreach (var deletedItem in result.DeletedItems)
                {
                    try
                    {
                        switch (deletedItem.EntityType.ToLower())
                        {
                            case "asset":
                                var asset = await dbContext.Assets.FindAsync(deletedItem.EntityId);
                                if (asset != null)
                                {
                                    dbContext.Assets.Remove(asset);
                                    deletedCount++;
                                    _logger.LogInformation("Removed deleted asset {AssetId} from local database", deletedItem.EntityId);
                                }
                                break;
                                
                            case "category":
                                var category = await dbContext.Categories.FindAsync(deletedItem.EntityId);
                                if (category != null)
                                {
                                    dbContext.Categories.Remove(category);
                                    deletedCount++;
                                    _logger.LogInformation("Removed deleted category {CategoryId} from local database", deletedItem.EntityId);
                                }
                                break;
                                
                            case "location":
                                var location = await dbContext.Locations.FindAsync(deletedItem.EntityId);
                                if (location != null)
                                {
                                    dbContext.Locations.Remove(location);
                                    deletedCount++;
                                    _logger.LogInformation("Removed deleted location {LocationId} from local database", deletedItem.EntityId);
                                }
                                break;
                                
                            case "department":
                                var department = await dbContext.Departments.FindAsync(deletedItem.EntityId);
                                if (department != null)
                                {
                                    dbContext.Departments.Remove(department);
                                    deletedCount++;
                                    _logger.LogInformation("Removed deleted department {DepartmentId} from local database", deletedItem.EntityId);
                                }
                                break;
                                
                            default:
                                _logger.LogWarning("Unknown entity type for deletion: {EntityType}", deletedItem.EntityType);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting {EntityType} {EntityId} from local database",
                            deletedItem.EntityType, deletedItem.EntityId);
                    }
                }
                
                // Save deletions
                if (deletedCount > 0)
                {
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("Removed {Count} deleted items from local database", deletedCount);
                }

                // ═══════════════════════════════════════════════════════════
                // STEP 6: Update last sync timestamp ONLY after successful sync
                // BUG FIX #2: Don't update LastSync if there are skipped assets
                // ═══════════════════════════════════════════════════════════
                if (skippedAssetIds.Any())
                {
                    _logger.LogWarning(
                        "Skipped {Count} assets due to missing references. LastSync NOT updated to prevent data loss. " +
                        "Skipped asset IDs: {AssetIds}",
                        skippedAssetIds.Count,
                        string.Join(", ", skippedAssetIds));
                    
                    var message = $"Synced {totalChanges} changes: " +
                                 $"{result.Categories.Count} categories, " +
                                 $"{result.Locations.Count} locations, " +
                                 $"{result.Departments.Count} departments, " +
                                 $"{result.Assets.Count - skippedAssetIds.Count} assets, " +
                                 $"{deletedCount} deleted items " +
                                 $"({skippedAssetIds.Count} skipped - will retry on next sync)";
                                 
                                 return (true, message);
                             }
                             else
                             {
                                 // Update LastSync timestamp BEFORE re-enabling change tracking
                                 // CRITICAL FIX: Must explicitly mark entity as modified because AutoDetectChangesEnabled is false
                                 // Without this, EF Core won't detect the change and won't save it to the database
                                 deviceInfo.LastSync = result.ServerTimestamp;
                                 dbContext.Entry(deviceInfo).Property(d => d.LastSync).IsModified = true;
                                 await dbContext.SaveChangesAsync();
             
                                 var message = $"Synced {totalChanges} changes: " +
                                              $"{result.Categories.Count} categories, " +
                                              $"{result.Locations.Count} locations, " +
                                              $"{result.Departments.Count} departments, " +
                                              $"{result.Assets.Count} assets, " +
                                              $"{deletedCount} deleted items";
             
                                 _logger.LogInformation("Pull sync completed successfully: {Message}", message);
                                 
                                 // FIX #6: Report completion
                                 ReportProgress(SyncPhase.Completed, totalChanges, totalChanges, message);
                                 
                                 return (true, message);
                                          }
                         }
                         finally
                         {
                             // ═══════════════════════════════════════════════════════════
                             // BUG FIX #5: ALWAYS re-enable change tracking after pull sync
                             // This ensures normal operation resumes even if errors occur
                             // ═══════════════════════════════════════════════════════════
                                if (dbContext != null)
                                {
                                    dbContext.ChangeTracker.AutoDetectChangesEnabled = true;
                                }
                         }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pulling changes");
            return (false, $"Pull error: {ex.Message}");
        }
        finally
        {
            // FIX #4: Always release semaphore, even on error
            _pullSemaphore.Release();
        }
    }

    public async Task<(bool Success, string Message)> FullSyncAsync()
    {
        _logger.LogInformation("Starting full sync (push + pull)");

        // FIX #4: Use dedicated full sync semaphore instead of the old _syncSemaphore
        var acquired = await _fullSyncSemaphore.WaitAsync(0).ConfigureAwait(false);
        if (!acquired)
        {
            _logger.LogWarning("Full sync already in progress - skipping concurrent request");
            return (false, "Sync already in progress");
        }
        try
        {
        // FIX #4: Push and Pull will use their own semaphores internally
        // This prevents deadlocks while still ensuring thread safety
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
        finally
        {
            // FIX #4: Release the full sync semaphore
            _fullSyncSemaphore.Release();
        }
    }

    public async Task<(bool Success, string Message)> EnqueueFullSyncAsync()
    {
        var tcs = new TaskCompletionSource<(bool, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new SyncWorkItem(SyncRequestType.Full, tcs);
        await _syncQueue.Writer.WriteAsync(item);
        return await tcs.Task;
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (var work in _syncQueue.Reader.ReadAllAsync())
        {
            try
            {
                (bool Success, string Message) result;
                if (work.Type == SyncRequestType.Push)
                {
                    result = await PushChangesAsync();
                }
                else
                {
                    // Full sync should be serialized via semaphore inside FullSyncAsync
                    result = await FullSyncAsync();
                }

                work.Tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing sync queue item");
                work.Tcs.TrySetException(ex);
            }
        }
    }

    public async Task<int> GetPendingSyncCountAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
        return await dbContext.SyncQueue.CountAsync();
    }

    private async Task<MobileData.Data.DeviceInfo> GetOrCreateDeviceInfoAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

        var deviceInfo = await dbContext.DeviceInfo.FirstOrDefaultAsync();
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
            dbContext.DeviceInfo.Add(deviceInfo);
            await dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Created new device info with ID: {DeviceId}, LastSync: {LastSync} (initial full sync)",
                deviceInfo.DeviceId, deviceInfo.LastSync);
        }
        return deviceInfo;
    }

    /// <summary>
    /// ENHANCEMENT #7: Queue an asset patch operation for bandwidth optimization
    /// Only changed fields are sent to server instead of the entire entity
    /// </summary>
    public async Task<(bool Success, string Message)> QueueAssetPatchAsync(string assetId, Dictionary<string, object?> changes)
    {
        if (string.IsNullOrEmpty(assetId))
            return (false, "Asset ID cannot be empty");

        if (changes == null || changes.Count == 0)
            return (false, "No changes to patch");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            // Create patch DTO with only changed fields
            var patch = new AssetPatchDTO
            {
                AssetId = assetId,
                Changes = changes,
                DateModified = DateTime.UtcNow
            };

            // Serialize patch to JSON
            var jsonData = System.Text.Json.JsonSerializer.Serialize(patch);

            // Create sync queue item
            var queueItem = new SyncQueueItem
            {
                EntityType = "Asset",
                EntityId = assetId,
                Operation = "PATCH",  // Server recognizes this operation type
                JsonData = jsonData,
                CreatedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            dbContext.SyncQueue.Add(queueItem);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "ENHANCEMENT #7: Queued PATCH for asset {AssetId} with {Count} changes ({Bytes} bytes - saves {Savings}% vs UPDATE)",
                assetId, changes.Count, patch.EstimatedSizeBytes, 
                ((2000 - patch.EstimatedSizeBytes) / 2000 * 100).ToString("F0"));

            return (true, $"Queued patch with {changes.Count} changes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing asset patch");
            return (false, $"Error queuing patch: {ex.Message}");
        }
    }

    /// <summary>
    /// ENHANCEMENT #7: Queue a full asset update
    /// Use when many fields changed (>50%) or for bulk operations
    /// </summary>
    public async Task<(bool Success, string Message)> QueueAssetUpdateAsync(Asset asset)
    {
        if (asset == null)
            return (false, "Asset cannot be null");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            // Create update DTO
            var updateDto = new AssetUpdateDTO
            {
                AssetId = asset.AssetId,
                AssetTag = asset.AssetTag,
                Name = asset.Name,
                Description = asset.Description,
                CategoryId = asset.CategoryId,
                LocationId = asset.LocationId,
                DepartmentId = asset.DepartmentId,
                PurchaseDate = asset.PurchaseDate,
                PurchasePrice = asset.PurchasePrice,
                CurrentValue = asset.CurrentValue,
                Status = asset.Status,
                AssignedToUserId = asset.AssignedToUserId,
                SerialNumber = asset.SerialNumber,
                DigitalAssetTag = asset.DigitalAssetTag,
                Condition = asset.Condition,
                VendorName = asset.VendorName,
                InvoiceNumber = asset.InvoiceNumber,
                Quantity = asset.Quantity,
                CostPerUnit = asset.CostPerUnit,
                UsefulLifeYears = asset.UsefulLifeYears,
                WarrantyExpiry = asset.WarrantyExpiry,
                DisposalDate = asset.DisposalDate,
                DisposalValue = asset.DisposalValue,
                Remarks = asset.Remarks,
                DateModified = DateTime.UtcNow
            };

            var jsonData = System.Text.Json.JsonSerializer.Serialize(updateDto);

            var queueItem = new SyncQueueItem
            {
                EntityType = "Asset",
                EntityId = asset.AssetId,
                Operation = "UPDATE",
                JsonData = jsonData,
                CreatedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            dbContext.SyncQueue.Add(queueItem);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Queued UPDATE for asset {AssetId} with all fields", asset.AssetId);

            return (true, "Queued full update");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing asset update");
            return (false, $"Error queuing update: {ex.Message}");
        }
    }

    /// <summary>
    /// ENHANCEMENT #7: Intelligently detect changed fields and queue appropriate operation
    /// Automatically chooses PATCH (for few changes) or UPDATE (for many changes)
    /// </summary>
    public async Task<(bool Success, string Message)> QueueAssetChangeAsync(Asset current, Asset original)
    {
        if (current == null || original == null)
            return (false, "Both current and original assets required");

        // Detect changed fields
        var changes = new Dictionary<string, object?>();

        // Check all properties that could be edited by users
        if (current.Status != original.Status && !string.IsNullOrEmpty(current.Status))
            changes["Status"] = current.Status;

        if (current.LocationId != original.LocationId && !string.IsNullOrEmpty(current.LocationId))
            changes["LocationId"] = current.LocationId;

        if (current.Condition != original.Condition && !string.IsNullOrEmpty(current.Condition))
            changes["Condition"] = current.Condition;

        if (current.AssignedToUserId != original.AssignedToUserId)
            changes["AssignedToUserId"] = current.AssignedToUserId;

        if (current.CurrentValue != original.CurrentValue && current.CurrentValue.HasValue)
            changes["CurrentValue"] = current.CurrentValue;

        if (current.Name != original.Name && !string.IsNullOrEmpty(current.Name))
            changes["Name"] = current.Name;

        if (current.Description != original.Description)
            changes["Description"] = current.Description;

        if (current.Remarks != original.Remarks)
            changes["Remarks"] = current.Remarks;

        if (!changes.Any())
            return (true, "No changes detected");

        // Smart decision: Use PATCH for few changes, UPDATE for many
        if (changes.Count <= 3)
        {
            _logger.LogInformation(
                "ENHANCEMENT #7: Few changes detected ({Count}), using PATCH for bandwidth optimization",
                changes.Count);
            return await QueueAssetPatchAsync(current.AssetId, changes);
        }
        else
        {
            _logger.LogInformation(
                "ENHANCEMENT #7: Many changes detected ({Count}), using UPDATE for simplicity",
                changes.Count);
            return await QueueAssetUpdateAsync(current);
        }
    }

    /// <summary>
    /// Reset sync state to force a full re-sync from server.
    /// Use this when local database is corrupted or out of sync.
    /// </summary>
    public async Task ResetSyncStateAsync()
    {
        _logger.LogWarning("Resetting sync state - will perform full re-sync on next pull");
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

        var deviceInfo = await dbContext.DeviceInfo.FirstOrDefaultAsync();
        if (deviceInfo != null)
        {
            // Reset to 1900 to fetch all data
            deviceInfo.LastSync = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            await dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Sync state reset. LastSync set to {LastSync}", deviceInfo.LastSync);
        }
    }

    /// <summary>
    /// Clear all local data from the mobile database.
    /// This will delete all assets, categories, locations, departments, and sync queue items.
    /// Does NOT sync with server - just clears local storage.
    /// </summary>
    public async Task ClearAllLocalDataAsync()
    {
        _logger.LogWarning("Clearing all local data from mobile database");
        
        try
        {
            // Use a scoped DbContext so deletions are isolated to this operation
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            // Delete data (this may create SyncQueue operations due to change tracking)
            dbContext.AssetHistories.RemoveRange(dbContext.AssetHistories);
            dbContext.Assets.RemoveRange(dbContext.Assets);
            dbContext.Categories.RemoveRange(dbContext.Categories);
            dbContext.Locations.RemoveRange(dbContext.Locations);
            dbContext.Departments.RemoveRange(dbContext.Departments);

            await dbContext.SaveChangesAsync();

            // NOW clear the SyncQueue (removes any operations created above)
            // This ensures we don't try to sync deletions of data we're clearing locally
            dbContext.SyncQueue.RemoveRange(dbContext.SyncQueue);
            await dbContext.SaveChangesAsync();

            // Reset sync state so next pull will fetch all data from server
            await ResetSyncStateAsync();

            _logger.LogInformation("All local data cleared successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing local data");
            throw;
        }
    }
}