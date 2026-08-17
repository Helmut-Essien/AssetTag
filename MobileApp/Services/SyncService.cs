using MobileData.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using Shared.Models;
using System.Net.Http.Json;
using System.Threading.Channels;

namespace MobileApp.Services;

public class SyncService : ISyncService, IDisposable
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
    // FIX #9: Semaphore to prevent race condition in GetOrCreateDeviceInfoAsync
    private readonly SemaphoreSlim _deviceInfoSemaphore = new(1, 1);
    
    // Channel-based queue to serialize background sync requests.
    // DropWrite rejects new items when full so callers never hang on a dropped TCS.
    private const int SyncChannelCapacity = 64;
    private readonly Channel<SyncWorkItem> _syncQueue = Channel.CreateBounded<SyncWorkItem>(new BoundedChannelOptions(SyncChannelCapacity)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropWrite
    });

    private readonly Task _queueProcessorTask;
    private readonly object _queueStateLock = new();
    private bool _pushQueuedOrRunning;
    private bool _fullQueuedOrRunning;
    private readonly List<TaskCompletionSource<(bool Success, string Message)>> _outstandingTcs = new();
    
    // FIX #12: Cancellation token for graceful shutdown
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _disposed;

    // Maximum retry attempts before preserving a sync item as blocked for manual review.
    private const int MAX_RETRY_COUNT = 5;

    private record SyncWorkItem(SyncRequestType Type, TaskCompletionSource<(bool Success, string Message)> Tcs);

    private enum SyncRequestType { Push, Full }

    public event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;

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
        // Start background processor for sync queue with cancellation support
        _queueProcessorTask = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
    }

    // HIGH FIX #5: Implement IDisposable for graceful shutdown and resource cleanup
    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            // Signal cancellation to background task
            _cancellationTokenSource.Cancel();
            _syncQueue.Writer.TryComplete();

            // Unblock any callers still awaiting enqueue results
            lock (_queueStateLock)
            {
                foreach (var tcs in _outstandingTcs)
                {
                    tcs.TrySetCanceled(_cancellationTokenSource.Token);
                }
                _outstandingTcs.Clear();
                _pushQueuedOrRunning = false;
                _fullQueuedOrRunning = false;
            }
            
            // Wait for background task to complete (with timeout to prevent hanging)
            if (!_queueProcessorTask.Wait(TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning("Background sync queue processor did not complete within timeout");
            }
            
            // HIGH FIX #5: Dispose the task to release resources and prevent memory leak
            _queueProcessorTask?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SyncService disposal");
        }
        finally
        {
            // Dispose resources
            _pushSemaphore?.Dispose();
            _pullSemaphore?.Dispose();
            _fullSyncSemaphore?.Dispose();
            _deviceInfoSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
            
            _disposed = true;
        }
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
            return await PushChangesInternalAsync();
        }
        finally
        {
            // FIX #4: Always release semaphore, even on error
            _pushSemaphore.Release();
        }
    }

    /// <summary>
    /// CRITICAL FIX #2: Internal push method without semaphore (called by FullSyncAsync)
    /// </summary>
    private async Task<(bool Success, string Message)> PushChangesInternalAsync()
    {
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
            var blockedCount = await dbContext.SyncQueue
                .CountAsync(s => s.RetryCount >= MAX_RETRY_COUNT);

            var pendingItems = await dbContext.SyncQueue
                .Where(s => s.RetryCount < MAX_RETRY_COUNT)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync();

            if (!pendingItems.Any())
            {
                _logger.LogInformation("Push sync: No changes to sync");
                ReportProgress(SyncPhase.Completed, 0, 0, "No changes to sync");
                return (true, blockedCount > 0
                    ? $"No retryable changes to sync ({blockedCount} blocked items need review)"
                    : "No changes to sync");
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
                    // FIX #7: Pass HTTP response for enhanced infrastructure failure detection
                    if (IsInfrastructureSyncFailure(result, response))
                    {
                        var infrastructureError = result.Metrics?.ErrorMessage
                            ?? result.Errors.FirstOrDefault()?.ErrorMessage
                            ?? "Server infrastructure failure";

                        _logger.LogError(
                            "Push sync infrastructure failure. Queue items will remain pending without retry count changes. Error: {Error}",
                            infrastructureError);

                        ReportProgress(SyncPhase.Failed, 0, pendingItems.Count,
                            "Server sync failed. Changes will retry later.");

                        return (false, $"Server sync failed: {infrastructureError}");
                    }

                    // BUG FIX #1: Only remove items that were successfully synced
                    // Get the items that succeeded based on the IDs returned from server
                    var successfulItems = pendingItems
                        .Where(item => result.SuccessfulOperationIds.Contains(item.Id))
                        .ToList();
                    
                    dbContext.SyncQueue.RemoveRange(successfulItems);
                    
                    // FIX #2: Increment retry count for failed items, but preserve them for review
                    var failedItems = pendingItems
                        .Where(item => !result.SuccessfulOperationIds.Contains(item.Id))
                        .ToList();
                    
                    var itemsToRetry = new List<SyncQueueItem>();
                    var itemsBlocked = new List<SyncQueueItem>();
                    
                    foreach (var failedItem in failedItems)
                    {
                        failedItem.RetryCount++;
                        
                        if (failedItem.RetryCount >= MAX_RETRY_COUNT)
                        {
                            failedItem.RetryCount = MAX_RETRY_COUNT;
                            itemsBlocked.Add(failedItem);
                            _logger.LogError(
                                "Max retry count ({MaxRetries}) exceeded for {EntityType} {EntityId}. " +
                                "Leaving item in sync queue as blocked for manual review. " +
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
                    
                    await SaveWithRetryAsync(dbContext, "push sync finalization");

                    var message = $"Synced {result.SuccessCount} changes, {itemsToRetry.Count} will retry, {itemsBlocked.Count + blockedCount} blocked for review";
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

            // FIX #7: Check if non-success status is an infrastructure failure
            var statusCode = (int)response.StatusCode;
            if (statusCode >= 500 || statusCode == 408 || statusCode == 429)
            {
                _logger.LogError(
                    "Push sync infrastructure failure: HTTP {StatusCode}. Queue items will remain pending without retry count changes.",
                    response.StatusCode);
                return (false, $"Server infrastructure failure: {response.StatusCode}");
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

    /// <summary>
    /// FIX #7: Enhanced infrastructure failure detection
    /// Checks both error messages and response status for infrastructure issues
    /// </summary>
    private bool IsInfrastructureSyncFailure(SyncPushResponseDTO result, HttpResponseMessage? response = null)
    {
        // FIX #7: Check HTTP status codes for infrastructure failures
        if (response != null)
        {
            var statusCode = (int)response.StatusCode;
            
            // 5xx errors are infrastructure failures
            if (statusCode >= 500 && statusCode < 600)
            {
                _logger.LogWarning("Infrastructure failure detected: HTTP {StatusCode}", statusCode);
                return true;
            }
            
            // 408 Request Timeout
            if (statusCode == 408)
            {
                _logger.LogWarning("Infrastructure failure detected: Request timeout (408)");
                return true;
            }
            
            // 429 Too Many Requests (rate limiting - infrastructure issue)
            if (statusCode == 429)
            {
                _logger.LogWarning("Infrastructure failure detected: Rate limited (429)");
                return true;
            }
        }
        
        // Check error messages for infrastructure keywords
        var messages = result.Errors
            .Select(error => error.ErrorMessage)
            .Append(result.Metrics?.ErrorMessage)
            .OfType<string>()
            .Where(message => !string.IsNullOrWhiteSpace(message));

        var hasInfrastructureKeyword = messages.Any(message =>
            message.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("transaction failed", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("execution strategy", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("transient", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
            
        if (hasInfrastructureKeyword)
        {
            _logger.LogWarning("Infrastructure failure detected in error messages");
        }
        
        return hasInfrastructureKeyword;
    }

    public Task<(bool Success, string Message)> EnqueuePushAsync()
    {
        lock (_queueStateLock)
        {
            if (_disposed)
                return Task.FromResult((false, "Sync service disposed"));

            // Coalesce with in-flight/queued push or full sync (full includes push)
            if (_pushQueuedOrRunning || _fullQueuedOrRunning)
            {
                _logger.LogDebug("Push coalesced — sync already queued or running");
                return Task.FromResult((true, "Sync already in progress"));
            }

            var tcs = new TaskCompletionSource<(bool Success, string Message)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var item = new SyncWorkItem(SyncRequestType.Push, tcs);

            if (!_syncQueue.Writer.TryWrite(item))
            {
                _logger.LogWarning("Sync queue full, rejecting push request");
                return Task.FromResult((false, "Sync queue busy, try again later"));
            }

            _pushQueuedOrRunning = true;
            _outstandingTcs.Add(tcs);
            return AwaitEnqueueAsync(tcs);
        }
    }

    public Task<(bool Success, string Message)> EnqueueFullSyncAsync()
    {
        lock (_queueStateLock)
        {
            if (_disposed)
                return Task.FromResult((false, "Sync service disposed"));

            if (_fullQueuedOrRunning)
            {
                _logger.LogDebug("Full sync coalesced — full sync already queued or running");
                return Task.FromResult((true, "Sync already in progress"));
            }

            var tcs = new TaskCompletionSource<(bool Success, string Message)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var item = new SyncWorkItem(SyncRequestType.Full, tcs);

            if (!_syncQueue.Writer.TryWrite(item))
            {
                _logger.LogWarning("Sync queue full, rejecting full sync request");
                return Task.FromResult((false, "Sync queue busy, try again later"));
            }

            _fullQueuedOrRunning = true;
            _outstandingTcs.Add(tcs);
            return AwaitEnqueueAsync(tcs);
        }
    }

    private static async Task<(bool Success, string Message)> AwaitEnqueueAsync(
        TaskCompletionSource<(bool Success, string Message)> tcs)
    {
        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return (false, "Sync cancelled");
        }
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
            return await PullChangesInternalAsync();
        }
        finally
        {
            // FIX #4: Always release semaphore, even on error
            _pullSemaphore.Release();
        }
    }

    /// <summary>
    /// CRITICAL FIX #2: Internal pull method without semaphore (called by FullSyncAsync)
    /// </summary>
    private async Task<(bool Success, string Message)> PullChangesInternalAsync()
    {
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

            var pendingLocalAssetIds = await dbContext.SyncQueue
                .AsNoTracking()
                .Where(s => s.EntityType == "Asset")
                .Select(s => s.EntityId)
                .ToHashSetAsync();

            try
            {
                dbContext.SuppressSyncQueue = true;
                
                var totalChanges = 0;
                var skippedAssetIds = new List<string>(); // BUG FIX #2: Track skipped assets
                var deferredAssetIds = new List<string>();

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

                // Prefetch reference ID sets once — avoids 3 AnyAsync queries per asset
                var knownCategoryIds = await dbContext.Categories
                    .AsNoTracking()
                    .Select(c => c.CategoryId)
                    .ToHashSetAsync();
                var knownLocationIds = await dbContext.Locations
                    .AsNoTracking()
                    .Select(l => l.LocationId)
                    .ToHashSetAsync();
                var knownDepartmentIds = await dbContext.Departments
                    .AsNoTracking()
                    .Select(d => d.DepartmentId)
                    .ToHashSetAsync();

                // Snapshot pending asset IDs; end-of-batch re-check still covers races
                var pendingAssetIds = await dbContext.SyncQueue
                    .AsNoTracking()
                    .Where(s => s.EntityType == "Asset")
                    .Select(s => s.EntityId)
                    .ToHashSetAsync();

                for (int offset = 0; offset < assets.Count; offset += ASSET_BATCH_SIZE)
                {
                    var batch = assets.Skip(offset).Take(ASSET_BATCH_SIZE).ToList();
                    var batchAssetIds = new List<string>();
                    var batchIds = batch.Select(a => a.AssetId).ToList();

                    var existingAssets = await dbContext.Assets
                        .Where(a => batchIds.Contains(a.AssetId))
                        .ToDictionaryAsync(a => a.AssetId);

                    var skippedById = await dbContext.SkippedAssets
                        .Where(s => batchIds.Contains(s.AssetId))
                        .ToDictionaryAsync(s => s.AssetId);

                    foreach (var assetDto in batch)
                    {
                        if (pendingAssetIds.Contains(assetDto.AssetId))
                        {
                            deferredAssetIds.Add(assetDto.AssetId);
                            _logger.LogInformation(
                                "Deferring pulled asset {AssetId} ({AssetTag}) because it has pending local changes (detected during pull)",
                                assetDto.AssetId, assetDto.AssetTag);
                            continue;
                        }

                        var categoryExists = knownCategoryIds.Contains(assetDto.CategoryId);
                        var locationExists = knownLocationIds.Contains(assetDto.LocationId);
                        var departmentExists = knownDepartmentIds.Contains(assetDto.DepartmentId);

                        if (!categoryExists || !locationExists || !departmentExists)
                        {
                            _logger.LogWarning(
                                "Skipping asset {AssetId} ({AssetTag}) - missing references: Category={CategoryExists}, Location={LocationExists}, Department={DepartmentExists}",
                                assetDto.AssetId, assetDto.AssetTag, categoryExists, locationExists, departmentExists);

                            skippedAssetIds.Add(assetDto.AssetId);

                            if (skippedById.TryGetValue(assetDto.AssetId, out var existingSkipped))
                            {
                                existingSkipped.RetryCount++;
                                existingSkipped.SkippedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                var skippedAsset = new MobileData.Data.SkippedAsset
                                {
                                    AssetId = assetDto.AssetId,
                                    AssetTag = assetDto.AssetTag,
                                    Reason = $"Missing references - Category: {categoryExists}, Location: {locationExists}, Department: {departmentExists}",
                                    SkippedAt = DateTime.UtcNow,
                                    RetryCount = 1,
                                    MissingCategoryId = !categoryExists ? assetDto.CategoryId : null,
                                    MissingLocationId = !locationExists ? assetDto.LocationId : null,
                                    MissingDepartmentId = !departmentExists ? assetDto.DepartmentId : null
                                };
                                dbContext.SkippedAssets.Add(skippedAsset);
                                skippedById[assetDto.AssetId] = skippedAsset;
                            }

                            continue;
                        }

                        if (existingAssets.TryGetValue(assetDto.AssetId, out var existing))
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

                            _logger.LogDebug("Updated asset: {AssetName} ({AssetTag})", assetDto.Name, assetDto.AssetTag);
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

                            dbContext.Assets.Add(newAsset);
                            existingAssets[assetDto.AssetId] = newAsset;
                            _logger.LogDebug("Added new asset: {AssetName} ({AssetTag})", assetDto.Name, assetDto.AssetTag);
                        }

                        if (skippedById.TryGetValue(assetDto.AssetId, out var previouslySkipped))
                        {
                            dbContext.SkippedAssets.Remove(previouslySkipped);
                            skippedById.Remove(assetDto.AssetId);
                            _logger.LogInformation("Removed asset {AssetId} from skipped assets - now synced successfully", assetDto.AssetId);
                        }

                        totalChanges++;
                        assetIndex++;
                        batchAssetIds.Add(assetDto.AssetId);

                        if (assetIndex % 10 == 0 || assetIndex == assets.Count)
                        {
                            ReportProgress(SyncPhase.PullingAssets, assetIndex, assets.Count,
                                $"Syncing assets: {assetIndex}/{assets.Count}");
                        }
                    }

                    // CRITICAL FIX #1: Re-check all assets in batch for pending changes before save
                    // This prevents race condition where user modifies asset after initial check
                    var newlyPendingIds = await dbContext.SyncQueue
                        .Where(s => s.EntityType == "Asset" && batchAssetIds.Contains(s.EntityId))
                        .Select(s => s.EntityId)
                        .ToListAsync();

                    if (newlyPendingIds.Any())
                    {
                        _logger.LogWarning(
                            "Detected {Count} assets with pending changes created during batch processing. " +
                            "Rolling back their changes to prevent data loss: {AssetIds}",
                            newlyPendingIds.Count, string.Join(", ", newlyPendingIds));

                        // Rollback changes for newly pending assets
                        foreach (var pendingId in newlyPendingIds)
                        {
                            var entry = dbContext.ChangeTracker.Entries<Asset>()
                                .FirstOrDefault(e => e.Entity.AssetId == pendingId);
                            if (entry != null)
                            {
                                entry.State = EntityState.Unchanged;
                                deferredAssetIds.Add(pendingId);
                                totalChanges--; // Don't count as synced
                            }
                        }
                    }

                    // FIX #5: Save each batch with proper error handling for partial failures
                    try
                    {
                        await dbContext.SaveChangesAsync();
                        _logger.LogDebug("Successfully saved batch at offset {Offset} with {Count} assets", offset, batch.Count);
                    }
                    catch (Exception ex)
                    {
                        if (!batch.Any())
                        {
                            _logger.LogError(ex, "Error saving empty asset batch at offset {Offset}. This should not happen.", offset);
                            throw; // Re-throw to fail fast
                        }
                        
                        _logger.LogError(ex, "Error saving asset batch at offset {Offset} with {Count} assets. Attempting individual saves to identify problematic assets.", 
                            offset, batch.Count);
                        
                        // HIGH FIX #3: Try to save each asset individually with transaction wrapping
                        var batchSuccessCount = 0;
                        var batchFailureCount = 0;
                        
                        foreach (var assetDto in batch)
                        {
                            // HIGH FIX #3: Use a transaction for each individual asset save
                            await using var individualTransaction = await dbContext.Database.BeginTransactionAsync();
                            try
                            {
                                // Re-apply the asset changes (they were rolled back)
                                var existing = await dbContext.Assets.FindAsync(assetDto.AssetId);
                                if (existing != null)
                                {
                                    // Update existing asset
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
                                }
                                else
                                {
                                    // Insert new asset
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
                                }
                                
                                await dbContext.SaveChangesAsync();
                                await individualTransaction.CommitAsync();
                                batchSuccessCount++;
                            }
                            catch (Exception individualEx)
                            {
                                await individualTransaction.RollbackAsync();
                                _logger.LogError(individualEx, 
                                    "Failed to save individual asset {AssetId} ({AssetTag}) in batch recovery. Skipping this asset.",
                                    assetDto.AssetId, assetDto.AssetTag);
                                batchFailureCount++;
                                
                                // Track as skipped for retry
                                try
                                {
                                    var skippedAsset = new MobileData.Data.SkippedAsset
                                    {
                                        AssetId = assetDto.AssetId,
                                        AssetTag = assetDto.AssetTag,
                                        Reason = $"Batch save failed, individual save also failed: {individualEx.Message}",
                                        SkippedAt = DateTime.UtcNow,
                                        RetryCount = 1
                                    };
                                    dbContext.SkippedAssets.Add(skippedAsset);
                                    await dbContext.SaveChangesAsync();
                                }
                                catch (Exception skipEx)
                                {
                                    _logger.LogError(skipEx,
                                        "Failed to save skipped-asset record for {AssetId} ({AssetTag})",
                                        assetDto.AssetId, assetDto.AssetTag);
                                }
                            }
                        }
                        
                        // Adjust totalChanges to reflect actual saved count
                        totalChanges -= batchFailureCount;
                        
                        _logger.LogWarning(
                            "Batch recovery completed: {Success} assets saved, {Failed} assets failed and skipped",
                            batchSuccessCount, batchFailureCount);
                    }
                }

                // ═══════════════════════════════════════════════════════════
                // STEP 5: Process deleted items (FIX #5)
                // Remove entities that were deleted on the server
                // FIX #6: Delete in correct order to avoid foreign key constraint violations
                // Order: Assets first (children), then Categories/Locations/Departments (parents)
                // CRITICAL: Change tracking is already disabled, ensuring deletions don't create sync queue entries
                // ═══════════════════════════════════════════════════════════
                ReportProgress(SyncPhase.Finalizing, 0, result.DeletedItems.Count,
                    $"Processing {result.DeletedItems.Count} deleted items...");
                
                var deletedCount = 0;
                
                // FIX #6: Phase 1 - Delete Assets first (they reference Categories/Locations/Departments)
                var assetDeletions = result.DeletedItems.Where(d => d.EntityType.Equals("asset", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var deletedItem in assetDeletions)
                {
                    try
                    {
                        var asset = await dbContext.Assets.FindAsync(deletedItem.EntityId);
                        if (asset != null)
                        {
                            dbContext.Assets.Remove(asset);
                            deletedCount++;
                            _logger.LogInformation("Removed deleted asset {AssetId} from local database", deletedItem.EntityId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting asset {EntityId} from local database", deletedItem.EntityId);
                    }
                }
                
                // Save asset deletions before proceeding to reference data
                if (assetDeletions.Any())
                {
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("Removed {Count} deleted assets from local database", assetDeletions.Count);
                }
                
                // FIX #6: Phase 2 - Delete reference data (Categories, Locations, Departments)
                // These can now be safely deleted since dependent assets are gone
                var referenceDeletions = result.DeletedItems
                    .Where(d => !d.EntityType.Equals("asset", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                    
                foreach (var deletedItem in referenceDeletions)
                {
                    try
                    {
                        switch (deletedItem.EntityType.ToLower())
                        {
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
                
                // Save reference data deletions
                if (referenceDeletions.Any())
                {
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("Removed {Count} deleted reference entities from local database", referenceDeletions.Count);
                }
                
                _logger.LogInformation("Total deleted items processed: {Count} ({Assets} assets, {References} reference entities)",
                    deletedCount, assetDeletions.Count, referenceDeletions.Count);

                // ═══════════════════════════════════════════════════════════
                // STEP 6: Update last sync timestamp only when all assets applied.
                // Advancing LastSync past skipped/deferred assets would drop them
                // from the server delta window permanently (no ID-based retry).
                // ═══════════════════════════════════════════════════════════
                var localDeviceInfo = await dbContext.DeviceInfo
                    .FirstAsync(d => d.Id == deviceInfo.Id);

                if (skippedAssetIds.Count == 0 && deferredAssetIds.Count == 0)
                {
                    localDeviceInfo.LastSync = result.ServerTimestamp;
                    await dbContext.SaveChangesAsync();
                }
                else
                {
                    _logger.LogWarning(
                        "Not advancing LastSync ({Skipped} skipped, {Deferred} deferred). " +
                        "Same delta window will be re-requested on the next pull.",
                        skippedAssetIds.Count, deferredAssetIds.Count);
                }

                if (skippedAssetIds.Any())
                {
                    _logger.LogWarning(
                        "Skipped {Count} assets due to missing references. " +
                        "LastSync left unchanged so they can be retried on the next pull. " +
                        "Skipped asset IDs: {AssetIds}",
                        skippedAssetIds.Count,
                        string.Join(", ", skippedAssetIds.Take(10)));
                }

                if (deferredAssetIds.Any())
                {
                    _logger.LogInformation(
                        "Deferred {Count} assets because they have pending local changes. " +
                        "LastSync left unchanged until push clears the queue and pull can apply them. " +
                        "Deferred asset IDs: {AssetIds}",
                        deferredAssetIds.Count,
                        string.Join(", ", deferredAssetIds.Take(10)));
                }

                var message = $"Synced {totalChanges} changes: " +
                             $"{result.Categories.Count} categories, " +
                             $"{result.Locations.Count} locations, " +
                             $"{result.Departments.Count} departments, " +
                             $"{result.Assets.Count - skippedAssetIds.Count - deferredAssetIds.Count} assets, " +
                             $"{deletedCount} deleted items";

                if (skippedAssetIds.Any() || deferredAssetIds.Any())
                {
                    message += $" ({skippedAssetIds.Count} skipped, {deferredAssetIds.Count} deferred)";
                }

                _logger.LogInformation("Pull sync completed successfully: {Message}", message);
                
                // FIX #6: Report completion
                ReportProgress(SyncPhase.Completed, totalChanges, totalChanges, message);
                
                return (true, message);
                         }
                         finally
                         {
                                if (dbContext != null)
                                {
                                    dbContext.SuppressSyncQueue = false;
                                }
                         }
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
        ReportProgress(SyncPhase.Starting, 0, 0, "Preparing sync...");

        // CRITICAL FIX #2: Acquire all semaphores upfront to prevent deadlock
        var fullSyncAcquired = await _fullSyncSemaphore.WaitAsync(0).ConfigureAwait(false);
        if (!fullSyncAcquired)
        {
            _logger.LogWarning("Full sync already in progress - skipping concurrent request");
            return (false, "Sync already in progress");
        }

        try
        {
            // Acquire both push and pull semaphores to prevent interleaving with direct calls
            await _pushSemaphore.WaitAsync();
            try
            {
                await _pullSemaphore.WaitAsync();
                try
                {
                    // Now we have exclusive access to both operations
                    _logger.LogInformation("Acquired all sync locks, starting push operation");
                    ReportProgress(SyncPhase.PushingChanges, 0, 0, "Pushing local changes...");
                    
                    var (pushSuccess, pushMessage) = await PushChangesInternalAsync();
                    if (!pushSuccess)
                    {
                        _logger.LogWarning("Full sync: Push failed - {Message}", pushMessage);
                        return (false, $"Push failed: {pushMessage}");
                    }

                    _logger.LogInformation("Push completed, starting pull operation");
                    ReportProgress(SyncPhase.PullingCategories, 0, 0, "Pulling server updates...");
                    
                    var (pullSuccess, pullMessage) = await PullChangesInternalAsync();
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
                    _pullSemaphore.Release();
                }
            }
            finally
            {
                _pushSemaphore.Release();
            }
        }
        finally
        {
            _fullSyncSemaphore.Release();
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var work in _syncQueue.Reader.ReadAllAsync(cancellationToken))
            {
                // Check for cancellation before processing
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Sync queue processor cancelled");
                    work.Tcs.TrySetCanceled(cancellationToken);
                    ClearQueueFlags(work);
                    break;
                }

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
                finally
                {
                    ClearQueueFlags(work);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Sync queue processor gracefully shut down");
        }
        finally
        {
            // Cancel any remaining queued work that will never be processed
            while (_syncQueue.Reader.TryRead(out var leftover))
            {
                leftover.Tcs.TrySetCanceled(cancellationToken);
                ClearQueueFlags(leftover);
            }
        }
    }

    private void ClearQueueFlags(SyncWorkItem work)
    {
        lock (_queueStateLock)
        {
            _outstandingTcs.Remove(work.Tcs);
            if (work.Type == SyncRequestType.Push)
                _pushQueuedOrRunning = false;
            else
                _fullQueuedOrRunning = false;
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
        // FIX #9: Prevent race condition when multiple syncs start simultaneously on first launch
        await _deviceInfoSemaphore.WaitAsync();
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            var deviceInfo = await dbContext.DeviceInfo
                .OrderBy(d => d.Id)
                .FirstOrDefaultAsync();
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
        finally
        {
            _deviceInfoSemaphore.Release();
        }
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

            // Use the patch contract with every syncable field so nulls are explicit field clears.
            var patchDto = new AssetPatchDTO
            {
                AssetId = asset.AssetId,
                DateModified = DateTime.UtcNow,
                Changes = new Dictionary<string, object?>
                {
                    [nameof(Asset.AssetTag)] = asset.AssetTag,
                    [nameof(Asset.Name)] = asset.Name,
                    [nameof(Asset.Description)] = asset.Description,
                    [nameof(Asset.CategoryId)] = asset.CategoryId,
                    [nameof(Asset.LocationId)] = asset.LocationId,
                    [nameof(Asset.DepartmentId)] = asset.DepartmentId,
                    [nameof(Asset.PurchaseDate)] = asset.PurchaseDate,
                    [nameof(Asset.PurchasePrice)] = asset.PurchasePrice,
                    [nameof(Asset.CurrentValue)] = asset.CurrentValue,
                    [nameof(Asset.Status)] = asset.Status,
                    [nameof(Asset.AssignedToUserId)] = asset.AssignedToUserId,
                    [nameof(Asset.SerialNumber)] = asset.SerialNumber,
                    [nameof(Asset.DigitalAssetTag)] = asset.DigitalAssetTag,
                    [nameof(Asset.Condition)] = asset.Condition,
                    [nameof(Asset.VendorName)] = asset.VendorName,
                    [nameof(Asset.InvoiceNumber)] = asset.InvoiceNumber,
                    [nameof(Asset.Quantity)] = asset.Quantity,
                    [nameof(Asset.CostPerUnit)] = asset.CostPerUnit,
                    [nameof(Asset.UsefulLifeYears)] = asset.UsefulLifeYears,
                    [nameof(Asset.WarrantyExpiry)] = asset.WarrantyExpiry,
                    [nameof(Asset.DisposalDate)] = asset.DisposalDate,
                    [nameof(Asset.DisposalValue)] = asset.DisposalValue,
                    [nameof(Asset.Remarks)] = asset.Remarks
                }
            };

            var jsonData = System.Text.Json.JsonSerializer.Serialize(patchDto);

            var queueItem = new SyncQueueItem
            {
                EntityType = "Asset",
                EntityId = asset.AssetId,
                Operation = "PATCH",
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

    private static readonly HashSet<string> s_syncableAssetProperties = new(StringComparer.Ordinal)
    {
        nameof(Asset.AssetTag),
        nameof(Asset.Name),
        nameof(Asset.Description),
        nameof(Asset.CategoryId),
        nameof(Asset.LocationId),
        nameof(Asset.DepartmentId),
        nameof(Asset.PurchaseDate),
        nameof(Asset.PurchasePrice),
        nameof(Asset.CurrentValue),
        nameof(Asset.Status),
        nameof(Asset.AssignedToUserId),
        nameof(Asset.SerialNumber),
        nameof(Asset.DigitalAssetTag),
        nameof(Asset.Condition),
        nameof(Asset.VendorName),
        nameof(Asset.InvoiceNumber),
        nameof(Asset.Quantity),
        nameof(Asset.CostPerUnit),
        nameof(Asset.UsefulLifeYears),
        nameof(Asset.WarrantyExpiry),
        nameof(Asset.DisposalDate),
        nameof(Asset.DisposalValue),
        nameof(Asset.Remarks),
    };

    /// <summary>
    /// ENHANCEMENT #7: Intelligently detect changed fields and queue appropriate operation
    /// Automatically chooses PATCH (for few changes) or UPDATE (for many changes)
    /// </summary>
    public async Task<(bool Success, string Message)> QueueAssetChangeAsync(Asset current, Asset original)
    {
        if (current == null || original == null)
            return (false, "Both current and original assets required");

        var changes = DetectAssetChanges(current, original);

        if (changes.Count == 0)
            return (true, "No changes detected");

        _logger.LogInformation(
            "ENHANCEMENT #7: Detected {Count} changed field(s), using PATCH",
            changes.Count);
        return await QueueAssetPatchAsync(current.AssetId, changes);
    }

    private static Dictionary<string, object?> DetectAssetChanges(Asset current, Asset original)
    {
        var changes = new Dictionary<string, object?>();

        foreach (var prop in s_syncableAssetProperties)
        {
            var property = typeof(Asset).GetProperty(prop);
            if (property is not { CanRead: true }) continue;

            var originalValue = property.GetValue(original);
            var currentValue = property.GetValue(current);

            if (Equals(originalValue, currentValue)) continue;

            changes[prop] = currentValue;
        }

        return changes;
    }

    private static async Task SaveWithRetryAsync(MobileData.Data.LocalDbContext dbContext, string operationName)
    {
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await dbContext.SaveChangesAsync();
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
                System.Diagnostics.Debug.WriteLine(
                    $"SaveWithRetry: {operationName} attempt {attempt} failed, retrying: {ex.Message}");
            }
        }

        await dbContext.SaveChangesAsync();
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

        var deviceInfo = await dbContext.DeviceInfo
            .OrderBy(d => d.Id)
            .FirstOrDefaultAsync();
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

            dbContext.SuppressSyncQueue = true;
            
            try
            {
                dbContext.AssetHistories.RemoveRange(dbContext.AssetHistories);
                dbContext.Assets.RemoveRange(dbContext.Assets);
                dbContext.Categories.RemoveRange(dbContext.Categories);
                dbContext.Locations.RemoveRange(dbContext.Locations);
                dbContext.Departments.RemoveRange(dbContext.Departments);

                await dbContext.SaveChangesAsync();

                // Clear the SyncQueue (any pending operations)
                dbContext.SyncQueue.RemoveRange(dbContext.SyncQueue);
                await dbContext.SaveChangesAsync();

                // Reset sync state so next pull will fetch all data from server
                await ResetSyncStateAsync();

                _logger.LogInformation("All local data cleared successfully");
            }
            finally
            {
                dbContext.SuppressSyncQueue = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing local data");
            throw;
        }
    }
}
