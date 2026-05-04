using AssetTag.Data;
using AssetTag.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Shared.Models;
using System.Text.Json;
using System.Security.Claims;

namespace AssetTag.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SyncController> _logger;
    private readonly IDistributedLockService _distributedLock;

    public SyncController(
        ApplicationDbContext context, 
        ILogger<SyncController> logger,
        IDistributedLockService distributedLock)
    {
        _context = context;
        _logger = logger;
        _distributedLock = distributedLock;
    }

    private void CreateAssetHistory(string assetId, string action, string description,
        string? oldLocationId = null, string? newLocationId = null,
        string? oldStatus = null, string? newStatus = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Cannot create asset history: User ID not found in claims");
            return;
        }

        var history = new AssetHistory
        {
            AssetId = assetId,
            UserId = userId,
            Action = action,
            Description = description,
            OldLocationId = oldLocationId,
            NewLocationId = newLocationId,
            OldStatus = oldStatus,
            NewStatus = newStatus
        };

        _context.AssetHistories.Add(history);
    }

    /// <summary>
    /// Push offline changes from mobile to server
    /// FIX #1: Added transaction wrapping for atomic all-or-nothing sync operations
    /// ENHANCEMENT #8: Added metrics collection for monitoring
    /// </summary>
    [HttpPost("push")]
    public async Task<ActionResult<SyncPushResponseDTO>> PushChanges([FromBody] SyncPushRequestDTO request)
    {
        // ARCHITECTURAL FIX A1: Acquire distributed lock to prevent concurrent sync from multiple devices
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "User ID not found" });
        }

        var lockKey = $"sync:user:{userId}";
        var lockAcquired = await _distributedLock.TryAcquireAsync(lockKey, TimeSpan.FromSeconds(30));

        if (!lockAcquired)
        {
            _logger.LogWarning("Sync lock already held for user {UserId}. Another device is syncing.", userId);
            return StatusCode(409, new 
            { 
                error = "Another device is currently syncing. Please wait and try again.",
                retryAfterSeconds = 5
            });
        }

        try
        {
            // ENHANCEMENT #8: Start metrics collection
            var startTime = DateTime.UtcNow;
            var conflictsDetected = 0;
            long bytesTransferred = 0;

            _logger.LogInformation("Processing push sync from device {DeviceId} with {Count} operations (lock acquired)",
                request.DeviceId, request.Operations.Count);

            // ENHANCEMENT #8: Calculate approximate bytes transferred
            foreach (var operation in request.Operations)
            {
                bytesTransferred += operation.JsonData.Length * 2; // UTF-16 encoding
            }

            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
            return await strategy.ExecuteAsync(async () =>
            {
                var successCount = 0;
                var errors = new List<SyncErrorDTO>();
                var successfulOperationIds = new List<int>();
                var failedOperationId = -1; // FIX #1: Track which operation caused the failure

                // FIX #1: Wrap all operations in a single transaction for atomicity.
                // The transaction must run inside EF's execution strategy because SQL Server retries are enabled.
                await using var transaction = await _context.Database.BeginTransactionAsync();

                foreach (var operation in request.Operations.OrderBy(o => o.CreatedAt))
                {
                    try
                    {
                        switch (operation.EntityType.ToLower())
                        {
                            case "asset":
                                var conflictDetected = await ProcessAssetOperation(operation);
                                if (conflictDetected) conflictsDetected++;
                                successCount++;
                                successfulOperationIds.Add(operation.QueueItemId);
                                break;

                            default:
                                throw new InvalidOperationException($"Unknown entity type: {operation.EntityType}");
                        }
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("Conflict"))
                    {
                        // ENHANCEMENT #8: Track conflicts separately
                        conflictsDetected++;
                        failedOperationId = operation.QueueItemId; // FIX #1: Track the specific failed operation
                        
                        _logger.LogWarning(ex, "Conflict detected for {EntityType} {EntityId} (QueueItemId: {QueueItemId})",
                            operation.EntityType, operation.EntityId, operation.QueueItemId);

                        errors.Add(new SyncErrorDTO
                        {
                            EntityId = operation.EntityId,
                            Operation = operation.Operation,
                            ErrorMessage = ex.Message
                        });

                        await transaction.RollbackAsync();

                        // FIX #1: Return ONLY the failed operation ID and all operations that came after it
                        // Operations before the failure were rolled back but should be retried
                        var failedOperationIds = request.Operations
                            .Where(op => op.QueueItemId >= failedOperationId)
                            .Select(op => op.QueueItemId)
                            .ToList();
                        
                        _logger.LogWarning(
                            "Transaction rolled back. Failed operation: {FailedId}. " +
                            "Marking {Count} operations for retry (failed + subsequent operations)",
                            failedOperationId, failedOperationIds.Count);

                        // ENHANCEMENT #8: Return metrics even on conflict
                        var conflictMetrics = CreateMetrics(startTime, request.Operations.Count,
                            0, errors.Count, 0, 0, conflictsDetected, bytesTransferred, false, ex.Message);

                        return Ok(new SyncPushResponseDTO
                        {
                            SuccessCount = 0,
                            FailureCount = errors.Count,
                            Errors = errors,
                            SuccessfulOperationIds = new List<int>(), // Empty - nothing succeeded
                            Metrics = conflictMetrics
                        });
                    }
                    catch (Exception ex) when (!IsInfrastructureException(ex))
                    {
                        failedOperationId = operation.QueueItemId; // FIX #1: Track the specific failed operation
                        
                        _logger.LogError(ex, "Validation or data error processing sync operation for {EntityType} {EntityId} (QueueItemId: {QueueItemId})",
                            operation.EntityType, operation.EntityId, operation.QueueItemId);

                        errors.Add(new SyncErrorDTO
                        {
                            EntityId = operation.EntityId,
                            Operation = operation.Operation,
                            ErrorMessage = ex.Message
                        });

                        // FIX #1: On error, rollback transaction and return partial results
                        // This prevents inconsistent state where some operations succeed and others fail
                        await transaction.RollbackAsync();

                        // FIX #1: Return ONLY the failed operation ID and all operations that came after it
                        // Operations before the failure were rolled back but should be retried
                        var failedOperationIds = request.Operations
                            .Where(op => op.QueueItemId >= failedOperationId)
                            .Select(op => op.QueueItemId)
                            .ToList();

                        _logger.LogWarning(
                            "Transaction rolled back due to operation error. Failed operation: {FailedId}. " +
                            "Marking {Count} operations for retry (failed + subsequent operations). " +
                            "{SuccessCount} operations before failure were rolled back and will be retried.",
                            failedOperationId, failedOperationIds.Count, successCount);

                        // ENHANCEMENT #8: Return metrics even on error
                        var errorMetrics = CreateMetrics(startTime, request.Operations.Count,
                            0, errors.Count, 0, 0, conflictsDetected, bytesTransferred, false, ex.Message);

                        return Ok(new SyncPushResponseDTO
                        {
                            SuccessCount = 0, // No operations committed due to rollback
                            FailureCount = errors.Count,
                            Errors = errors,
                            SuccessfulOperationIds = new List<int>(), // Empty - nothing was committed
                            Metrics = errorMetrics
                        });
                    }
                }

                // FIX #1: Commit transaction only if ALL operations succeeded
                await transaction.CommitAsync();

                _logger.LogInformation("Push sync transaction committed: {SuccessCount} successful, {FailureCount} failed",
                    successCount, errors.Count);

                // ENHANCEMENT #8: Create success metrics
                var successMetrics = CreateMetrics(startTime, request.Operations.Count,
                    successCount, errors.Count, 0, 0, conflictsDetected, bytesTransferred, true, null);

                _logger.LogInformation(
                    "Push sync metrics: Duration={Duration}ms, Succeeded={Succeeded}, " +
                    "Failed={Failed}, Conflicts={Conflicts}, Bytes={Bytes}",
                    successMetrics.Duration.TotalMilliseconds,
                    successMetrics.PushOperationsSucceeded,
                    successMetrics.PushOperationsFailed,
                    successMetrics.ConflictsDetected,
                    successMetrics.BytesTransferred);

                return Ok(new SyncPushResponseDTO
                {
                    SuccessCount = successCount,
                    FailureCount = errors.Count,
                    Errors = errors,
                    SuccessfulOperationIds = successfulOperationIds,
                    Metrics = successMetrics
                });
            });
        }
        catch (Exception ex)
        {
            // FIX #1: Catch infrastructure errors after EF's execution strategy has retried them.
            _logger.LogError(ex, "Infrastructure error during push sync transaction");

            // ENHANCEMENT #8: Return metrics even on unexpected error
            var unexpectedErrorMetrics = CreateMetrics(startTime, request.Operations.Count,
                0, request.Operations.Count, 0, 0, conflictsDetected, bytesTransferred, false,
                $"Infrastructure transaction failed: {ex.Message}");

            return StatusCode(500, new SyncPushResponseDTO
            {
                SuccessCount = 0,
                FailureCount = request.Operations.Count,
                Errors = new List<SyncErrorDTO>
                {
                    new SyncErrorDTO
                    {
                        EntityId = "TRANSACTION",
                        Operation = "PUSH",
                        ErrorMessage = $"Infrastructure transaction failed: {ex.Message}"
                    }
                },
                SuccessfulOperationIds = new List<int>(),
                Metrics = unexpectedErrorMetrics
            });
            }
        }
        finally
        {
            // ARCHITECTURAL FIX A1: Always release distributed lock
            await _distributedLock.ReleaseAsync(lockKey);
            _logger.LogInformation("Released sync lock for user {UserId}", userId);
        }
    }

    private static bool IsInfrastructureException(Exception ex)
    {
        return ex is TimeoutException ||
            ex.Message.Contains("execution strategy", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("transaction failed", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("transient", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ENHANCEMENT #8: Helper method to create sync metrics
    /// </summary>
    private SyncMetrics CreateMetrics(DateTime startTime, int attempted, int succeeded, int failed,
        int retried, int permanentlyFailed, int conflicts, long bytes, bool success, string? errorMessage)
    {
        return new SyncMetrics
        {
            StartTime = startTime,
            EndTime = DateTime.UtcNow,
            PushOperationsAttempted = attempted,
            PushOperationsSucceeded = succeeded,
            PushOperationsFailed = failed,
            PushOperationsRetried = retried,
            PushOperationsPermanentlyFailed = permanentlyFailed,
            ConflictsDetected = conflicts,
            ConflictsResolved = 0, // Conflicts are not auto-resolved, mobile must pull latest
            BytesTransferred = bytes,
            Success = success,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Pull delta changes from server to mobile
    /// ENHANCEMENT #8: Added metrics collection for monitoring
    /// </summary>
    [HttpPost("pull")]
    public async Task<ActionResult<SyncPullResponseDTO>> PullChanges([FromBody] SyncPullRequestDTO request)
    {
        // ENHANCEMENT #8: Start metrics collection
        var startTime = DateTime.UtcNow;
        
        try
        {
            var lastSync = request.LastSyncTimestamp ?? DateTime.MinValue;
            var serverSnapshotUtc = DateTime.UtcNow;

            _logger.LogInformation("Processing pull sync for device {DeviceId} since {LastSync}",
                request.DeviceId, lastSync);

            // Get all assets modified after last sync
            // Note: Don't use .Include() to avoid circular reference issues
            // DTOs will map the foreign key IDs without loading navigation properties
            var assets = await _context.Assets
                .Where(a => a.DateModified >= lastSync && a.DateModified <= serverSnapshotUtc)
                .ToListAsync();

            // Get categories that were modified OR are referenced by the assets being synced
            var modifiedCategories = await _context.Categories
                .Where(c => c.DateModified >= lastSync && c.DateModified <= serverSnapshotUtc)
                .ToListAsync();

            var referencedCategoryIds = assets
                .Select(a => a.CategoryId)
                .Distinct()
                .ToList();

            _logger.LogInformation("Assets reference {Count} unique categories: {CategoryIds}",
                referencedCategoryIds.Count, string.Join(", ", referencedCategoryIds));

            var referencedCategories = await _context.Categories
                .Where(c => referencedCategoryIds.Contains(c.CategoryId))
                .ToListAsync();

            _logger.LogInformation("Found {Count} referenced categories in database", referencedCategories.Count);

            var categories = modifiedCategories
                .Union(referencedCategories)
                .DistinctBy(c => c.CategoryId)
                .ToList();

            _logger.LogInformation("Total categories to sync: {Modified} modified + {Referenced} referenced = {Total} total",
                modifiedCategories.Count, referencedCategories.Count, categories.Count);

            // Get locations that were modified OR are referenced by the assets being synced
            var modifiedLocations = await _context.Locations
                .Where(l => l.DateModified >= lastSync && l.DateModified <= serverSnapshotUtc)
                .ToListAsync();

            var referencedLocationIds = assets
                .Select(a => a.LocationId)
                .Distinct()
                .ToList();

            var referencedLocations = await _context.Locations
                .Where(l => referencedLocationIds.Contains(l.LocationId))
                .ToListAsync();

            var locations = modifiedLocations
                .Union(referencedLocations)
                .DistinctBy(l => l.LocationId)
                .ToList();

            // Get departments that were modified OR are referenced by the assets being synced
            var modifiedDepartments = await _context.Departments
                .Where(d => d.DateModified >= lastSync && d.DateModified <= serverSnapshotUtc)
                .ToListAsync();

            var referencedDepartmentIds = assets
                .Select(a => a.DepartmentId)
                .Distinct()
                .ToList();

            var referencedDepartments = await _context.Departments
                .Where(d => referencedDepartmentIds.Contains(d.DepartmentId))
                .ToListAsync();

            var departments = modifiedDepartments
                .Union(referencedDepartments)
                .DistinctBy(d => d.DepartmentId)
                .ToList();

            // FIX #5: Get deleted items since last sync
            var deletedItems = await _context.DeletedItems
                .Where(d => d.DeletedAt >= lastSync && d.DeletedAt <= serverSnapshotUtc)
                .OrderBy(d => d.DeletedAt)
                .ToListAsync();

            _logger.LogInformation(
                "Pull sync returning: {AssetCount} assets, {CategoryCount} categories, " +
                "{LocationCount} locations, {DepartmentCount} departments, {DeletedCount} deleted items",
                assets.Count, categories.Count, locations.Count, departments.Count, deletedItems.Count);

            // ENHANCEMENT #8: Calculate approximate response size for bandwidth metrics
            long bytesTransferred = EstimateResponseSize(assets, categories, locations, departments, deletedItems);

            // ENHANCEMENT #8: Create pull metrics
            var pullMetrics = new SyncMetrics
            {
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                PullCategoriesReceived = categories.Count,
                PullLocationsReceived = locations.Count,
                PullDepartmentsReceived = departments.Count,
                PullAssetsReceived = assets.Count,
                PullDeletedItemsReceived = deletedItems.Count,
                BytesTransferred = bytesTransferred,
                Success = true
            };

            _logger.LogInformation(
                "Pull sync metrics: Duration={Duration}ms, Categories={Categories}, " +
                "Locations={Locations}, Departments={Departments}, Assets={Assets}, " +
                "Deleted={Deleted}, Bytes={Bytes}",
                pullMetrics.Duration.TotalMilliseconds,
                pullMetrics.PullCategoriesReceived,
                pullMetrics.PullLocationsReceived,
                pullMetrics.PullDepartmentsReceived,
                pullMetrics.PullAssetsReceived,
                pullMetrics.PullDeletedItemsReceived,
                pullMetrics.BytesTransferred);

            return Ok(new SyncPullResponseDTO
            {
                Assets = assets.Select(MapAssetToDto).ToList(),
                Categories = categories.Select(c => new CategoryReadDTO(
                    c.CategoryId,
                    c.Name,
                    c.Description,
                    c.DepreciationRate
                )).ToList(),
                Locations = locations.Select(l => new LocationReadDTO(
                    l.LocationId,
                    l.Name,
                    l.Description,
                    l.Campus,
                    l.Building,
                    l.Room,
                    l.Latitude,
                    l.Longitude
                )).ToList(),
                Departments = departments.Select(d => new DepartmentReadDTO(
                    d.DepartmentId,
                    d.Name,
                    d.Description
                )).ToList(),
                DeletedItems = deletedItems.Select(d => new DeletedItemDTO(
                    d.EntityType,
                    d.EntityId,
                    d.DeletedAt
                )).ToList(),
                ServerTimestamp = serverSnapshotUtc,
                Metrics = pullMetrics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pull sync");
            return StatusCode(500, new { error = "Sync failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Process a single asset operation (CREATE, UPDATE, PATCH, DELETE)
    /// ENHANCEMENT #8: Returns true if a conflict was detected (for metrics)
    /// </summary>
    private async Task<bool> ProcessAssetOperation(SyncOperationDTO operation)
    {
        var conflictDetected = false;
        
        switch (operation.Operation.ToUpper())
        {
            case "CREATE":
                var createDto = JsonSerializer.Deserialize<AssetCreateDTO>(operation.JsonData);
                if (createDto == null) throw new Exception("Invalid asset data");

                // Idempotency check - don't create if already exists
                if (await _context.Assets.AnyAsync(a => a.AssetId == operation.EntityId))
                {
                    _logger.LogInformation("Asset {AssetId} already exists, skipping create", operation.EntityId);
                    return conflictDetected;
                }

                var createdAt = operation.CreatedAt == default ? DateTime.UtcNow : operation.CreatedAt;
                var newAsset = new Asset
                {
                    AssetId = operation.EntityId, // Use ULID from mobile
                    AssetTag = createDto.AssetTag,
                    Name = createDto.Name,
                    Description = createDto.Description,
                    CategoryId = createDto.CategoryId,
                    LocationId = createDto.LocationId,
                    DepartmentId = createDto.DepartmentId,
                    PurchaseDate = createDto.PurchaseDate,
                    PurchasePrice = createDto.PurchasePrice,
                    CurrentValue = createDto.CurrentValue,
                    Status = createDto.Status,
                    AssignedToUserId = createDto.AssignedToUserId,
                    SerialNumber = createDto.SerialNumber,
                    DigitalAssetTag = createDto.DigitalAssetTag,
                    Condition = createDto.Condition,
                    VendorName = createDto.VendorName,
                    InvoiceNumber = createDto.InvoiceNumber,
                    Quantity = createDto.Quantity,
                    CostPerUnit = createDto.CostPerUnit,
                    UsefulLifeYears = createDto.UsefulLifeYears,
                    WarrantyExpiry = createDto.WarrantyExpiry,
                    DisposalDate = createDto.DisposalDate,
                    DisposalValue = createDto.DisposalValue,
                    Remarks = createDto.Remarks,
                    CreatedAt = createdAt,
                    DateModified = createdAt
                };

                _context.Assets.Add(newAsset);
                await _context.SaveChangesAsync();
                
                // Record creation history
                CreateAssetHistory(
                    newAsset.AssetId,
                    "CREATE",
                    $"Asset '{newAsset.Name}' with tag '{newAsset.AssetTag}' was created via mobile sync",
                    newLocationId: newAsset.LocationId,
                    newStatus: newAsset.Status
                );
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Created asset {AssetId} via sync", operation.EntityId);
                break;

            case "UPDATE":
                var updateDto = JsonSerializer.Deserialize<AssetUpdateDTO>(operation.JsonData);
                if (updateDto == null) throw new Exception("Invalid asset data");

                var existingAsset = await _context.Assets.FindAsync(operation.EntityId);
                if (existingAsset == null)
                {
                    _logger.LogWarning("Asset {AssetId} not found for update", operation.EntityId);
                    return conflictDetected;
                }

                // HIGH FIX #4: Enhanced conflict detection - check both timestamp AND content
                // Only reject if timestamp is older AND content actually differs
                // IMPORTANT: Properly detect null value changes (intentional field clears)
                if (updateDto.DateModified.HasValue && updateDto.DateModified.Value < existingAsset.DateModified)
                {
                    // HIGH FIX #4: Check if content actually differs, including null value changes
                    var hasContentChanges = 
                        HasFieldChanged(updateDto.Name, existingAsset.Name) ||
                        HasFieldChanged(updateDto.Status, existingAsset.Status) ||
                        HasFieldChanged(updateDto.LocationId, existingAsset.LocationId) ||
                        HasFieldChanged(updateDto.Condition, existingAsset.Condition) ||
                        HasFieldChanged(updateDto.Description, existingAsset.Description) ||
                        HasFieldChanged(updateDto.CategoryId, existingAsset.CategoryId) ||
                        HasFieldChanged(updateDto.DepartmentId, existingAsset.DepartmentId) ||
                        HasFieldChanged(updateDto.CurrentValue, existingAsset.CurrentValue) ||
                        HasFieldChanged(updateDto.AssignedToUserId, existingAsset.AssignedToUserId) ||
                        HasFieldChanged(updateDto.Remarks, existingAsset.Remarks);
                    
                    if (hasContentChanges)
                    {
                        conflictDetected = true;
                        _logger.LogWarning(
                            "Conflict detected for asset {AssetId}: Mobile timestamp ({MobileTime}) is older than server timestamp ({ServerTime}) " +
                            "AND content differs. Rejecting update to prevent data loss. Mobile needs to pull latest changes.",
                            operation.EntityId, updateDto.DateModified.Value, existingAsset.DateModified);
                        
                        throw new InvalidOperationException(
                            $"Conflict: Server has newer version (Server: {existingAsset.DateModified:O}, Mobile: {updateDto.DateModified:O}) " +
                            "with different content. Please sync to get latest changes before updating.");
                    }
                    else
                    {
                        // FIX #4: Same content, different timestamp - allow update (no real conflict)
                        _logger.LogInformation(
                            "Asset {AssetId}: Mobile timestamp is older but content is identical. Allowing update (no real conflict).",
                            operation.EntityId);
                    }
                }

                // Store old values for history tracking
                var oldName = existingAsset.Name;
                var oldLocationId = existingAsset.LocationId;
                var oldStatus = existingAsset.Status;

                var changes = new List<string>();
                if (updateDto.Name != null && updateDto.Name != oldName)
                    changes.Add($"Name changed from '{oldName}' to '{updateDto.Name}'");

                // FIX #3: Apply updates only if mobile version is newer (Last-Write-Wins with timestamp validation)
                if (updateDto.AssetTag != null) existingAsset.AssetTag = updateDto.AssetTag;
                if (updateDto.Name != null) existingAsset.Name = updateDto.Name;
                if (updateDto.Description != null) existingAsset.Description = updateDto.Description;
                if (updateDto.CategoryId != null) existingAsset.CategoryId = updateDto.CategoryId;
                if (updateDto.LocationId != null) existingAsset.LocationId = updateDto.LocationId;
                if (updateDto.DepartmentId != null) existingAsset.DepartmentId = updateDto.DepartmentId;
                if (updateDto.PurchaseDate != null) existingAsset.PurchaseDate = updateDto.PurchaseDate;
                if (updateDto.PurchasePrice != null) existingAsset.PurchasePrice = updateDto.PurchasePrice;
                if (updateDto.CurrentValue != null) existingAsset.CurrentValue = updateDto.CurrentValue;
                if (updateDto.Status != null) existingAsset.Status = updateDto.Status;
                if (updateDto.AssignedToUserId != null) existingAsset.AssignedToUserId = updateDto.AssignedToUserId;
                if (updateDto.SerialNumber != null) existingAsset.SerialNumber = updateDto.SerialNumber;
                if (updateDto.DigitalAssetTag != null) existingAsset.DigitalAssetTag = updateDto.DigitalAssetTag;
                if (updateDto.Condition != null) existingAsset.Condition = updateDto.Condition;
                if (updateDto.VendorName != null) existingAsset.VendorName = updateDto.VendorName;
                if (updateDto.InvoiceNumber != null) existingAsset.InvoiceNumber = updateDto.InvoiceNumber;
                if (updateDto.Quantity.HasValue) existingAsset.Quantity = updateDto.Quantity.Value;
                if (updateDto.CostPerUnit != null) existingAsset.CostPerUnit = updateDto.CostPerUnit;
                if (updateDto.UsefulLifeYears.HasValue) existingAsset.UsefulLifeYears = updateDto.UsefulLifeYears.Value;
                if (updateDto.WarrantyExpiry != null) existingAsset.WarrantyExpiry = updateDto.WarrantyExpiry;
                if (updateDto.DisposalDate != null) existingAsset.DisposalDate = updateDto.DisposalDate;
                if (updateDto.DisposalValue != null) existingAsset.DisposalValue = updateDto.DisposalValue;
                if (updateDto.Remarks != null) existingAsset.Remarks = updateDto.Remarks;

                // FIX #3: Use mobile's DateModified if provided, otherwise use current server time
                existingAsset.DateModified = updateDto.DateModified ?? DateTime.UtcNow;
                
                if (updateDto.LocationId != null && updateDto.LocationId != oldLocationId)
                {
                    var oldLocation = oldLocationId != null ? await _context.Locations.FindAsync(oldLocationId) : null;
                    var newLocation = await _context.Locations.FindAsync(updateDto.LocationId);
                    changes.Add($"Location changed from '{oldLocation?.Name ?? "Unknown"}' to '{newLocation?.Name ?? "Unknown"}'");
                }
                
                if (updateDto.Status != null && updateDto.Status != oldStatus)
                    changes.Add($"Status changed from '{oldStatus}' to '{updateDto.Status}'");
                
                if (updateDto.Condition != null)
                    changes.Add($"Condition changed to '{updateDto.Condition}'");
                
                await _context.SaveChangesAsync();
                
                // Record update history if there were changes
                if (changes.Any())
                {
                    CreateAssetHistory(
                        existingAsset.AssetId,
                        "UPDATE",
                        $"Asset updated via mobile sync: {string.Join("; ", changes)}",
                        oldLocationId: oldLocationId,
                        newLocationId: existingAsset.LocationId,
                        oldStatus: oldStatus,
                        newStatus: existingAsset.Status
                    );
                    await _context.SaveChangesAsync();
                }
                
                _logger.LogInformation("Updated asset {AssetId} via sync", operation.EntityId);
                break;

            case "PATCH": // ENHANCEMENT #7: Handle patch operations for bandwidth optimization
                var patchDto = JsonSerializer.Deserialize<AssetPatchDTO>(operation.JsonData);
                if (patchDto == null) throw new Exception("Invalid patch data");

                var assetToPatch = await _context.Assets.FindAsync(operation.EntityId);
                if (assetToPatch == null)
                {
                    _logger.LogWarning("Asset {AssetId} not found for patch", operation.EntityId);
                    return conflictDetected;
                }

                // FIX #4: Enhanced conflict detection for PATCH - check both timestamp AND content
                if (patchDto.DateModified.HasValue && patchDto.DateModified.Value < assetToPatch.DateModified)
                {
                    // Check if any of the patched fields actually differ from current values
                    var hasContentChanges = false;
                    foreach (var change in patchDto.Changes)
                    {
                        var property = typeof(Asset).GetProperty(change.Key);
                        if (property != null && property.CanRead)
                        {
                            var currentValue = property.GetValue(assetToPatch);
                            var newValue = ConvertPatchValue(change.Value, property.PropertyType);
                            
                            // Compare values (handle nulls)
                            if ((currentValue == null && newValue != null) ||
                                (currentValue != null && !currentValue.Equals(newValue)))
                            {
                                hasContentChanges = true;
                                break;
                            }
                        }
                    }
                    
                    if (hasContentChanges)
                    {
                        conflictDetected = true;
                        _logger.LogWarning(
                            "Conflict detected for asset {AssetId}: Mobile timestamp ({MobileTime}) is older than server timestamp ({ServerTime}) " +
                            "AND patched content differs. Rejecting patch to prevent data loss.",
                            operation.EntityId, patchDto.DateModified.Value, assetToPatch.DateModified);
                        
                        throw new InvalidOperationException(
                            $"Conflict: Server has newer version (Server: {assetToPatch.DateModified:O}, Mobile: {patchDto.DateModified:O}) " +
                            "with different content. Please sync to get latest changes before updating.");
                    }
                    else
                    {
                        // FIX #4: Same content, different timestamp - allow patch (no real conflict)
                        _logger.LogInformation(
                            "Asset {AssetId}: Mobile timestamp is older but patched content is identical. Allowing patch (no real conflict).",
                            operation.EntityId);
                    }
                }

                // Store old values for history tracking
                var patchOldLocationId = assetToPatch.LocationId;
                var patchOldStatus = assetToPatch.Status;
                var patchChanges = new List<string>();

                // Apply only changed fields using reflection
                foreach (var change in patchDto.Changes)
                {
                    var property = typeof(Asset).GetProperty(change.Key);
                    if (property != null && property.CanWrite && IsSyncPatchableAssetProperty(change.Key))
                    {
                        var oldValue = property.GetValue(assetToPatch);
                        var newValue = ConvertPatchValue(change.Value, property.PropertyType);
                        property.SetValue(assetToPatch, newValue);
                        patchChanges.Add($"{change.Key} changed from '{oldValue}' to '{newValue}'");
                        
                        _logger.LogDebug("Patched {Property} on asset {AssetId}: {OldValue} -> {NewValue}",
                            change.Key, operation.EntityId, oldValue, newValue);
                    }
                }

                assetToPatch.DateModified = patchDto.DateModified ?? DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Record patch history
                if (patchChanges.Any())
                {
                    CreateAssetHistory(
                        assetToPatch.AssetId,
                        "PATCH",
                        $"Asset patched via mobile sync: {string.Join("; ", patchChanges)}",
                        oldLocationId: patchOldLocationId,
                        newLocationId: assetToPatch.LocationId,
                        oldStatus: patchOldStatus,
                        newStatus: assetToPatch.Status
                    );
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Patched asset {AssetId} with {Count} changes ({Bytes} bytes saved vs full update)",
                    operation.EntityId, patchDto.Changes.Count, patchDto.EstimatedSizeBytes);
                break;

            case "DELETE":
                var assetToDelete = await _context.Assets.FindAsync(operation.EntityId);
                if (assetToDelete != null)
                {
                    var assetTag = assetToDelete.AssetTag;
                    var assetName = assetToDelete.Name;
                    
                    // Record deletion history BEFORE deleting the asset
                    CreateAssetHistory(
                        assetToDelete.AssetId,
                        "DELETE",
                        $"Asset '{assetName}' with tag '{assetTag}' was deleted via mobile sync"
                    );
                    await _context.SaveChangesAsync();
                    
                    // FIX #5: Deletion tracking now handled automatically by ApplicationDbContext.SaveChanges()
                    // The DbContext intercepts EntityState.Deleted and creates DeletedItem records
                    _context.Assets.Remove(assetToDelete);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Deleted asset {AssetId} via sync and recorded in DeletedItems", operation.EntityId);
                }
                break;
        }
        
        return conflictDetected;
    }

    private static bool IsSyncPatchableAssetProperty(string propertyName)
    {
        return propertyName is
            nameof(Asset.AssetTag) or
            nameof(Asset.Name) or
            nameof(Asset.Description) or
            nameof(Asset.CategoryId) or
            nameof(Asset.LocationId) or
            nameof(Asset.DepartmentId) or
            nameof(Asset.PurchaseDate) or
            nameof(Asset.PurchasePrice) or
            nameof(Asset.CurrentValue) or
            nameof(Asset.Status) or
            nameof(Asset.AssignedToUserId) or
            nameof(Asset.SerialNumber) or
            nameof(Asset.DigitalAssetTag) or
            nameof(Asset.Condition) or
            nameof(Asset.VendorName) or
            nameof(Asset.InvoiceNumber) or
            nameof(Asset.Quantity) or
            nameof(Asset.CostPerUnit) or
            nameof(Asset.UsefulLifeYears) or
            nameof(Asset.WarrantyExpiry) or
            nameof(Asset.DisposalDate) or
            nameof(Asset.DisposalValue) or
            nameof(Asset.Remarks);
    }

    private static object? ConvertPatchValue(object? value, Type targetType)
    {
        var nullableType = Nullable.GetUnderlyingType(targetType);
        var effectiveType = nullableType ?? targetType;

        if (value == null)
        {
            if (nullableType != null || !targetType.IsValueType)
            {
                return null;
            }

            throw new InvalidOperationException($"Cannot set non-nullable property of type {targetType.Name} to null.");
        }

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                if (nullableType != null || !targetType.IsValueType)
                {
                    return null;
                }

                throw new InvalidOperationException($"Cannot set non-nullable property of type {targetType.Name} to null.");
            }

            return jsonElement.Deserialize(effectiveType);
        }

        if (effectiveType.IsInstanceOfType(value))
        {
            return value;
        }

        return Convert.ChangeType(value, effectiveType);
    }

    /// <summary>
    /// ENHANCEMENT #8: Estimate response size for bandwidth metrics
    /// </summary>
    private long EstimateResponseSize(List<Asset> assets, List<Category> categories,
        List<Location> locations, List<Department> departments, List<DeletedItem> deletedItems)
    {
        // Rough estimates based on average entity sizes (in bytes)
        const int ASSET_AVG_SIZE = 2000;      // ~2KB per asset (with all fields)
        const int CATEGORY_AVG_SIZE = 200;    // ~200 bytes per category
        const int LOCATION_AVG_SIZE = 300;    // ~300 bytes per location
        const int DEPARTMENT_AVG_SIZE = 200;  // ~200 bytes per department
        const int DELETED_ITEM_AVG_SIZE = 100; // ~100 bytes per deleted item

        return (assets.Count * ASSET_AVG_SIZE) +
               (categories.Count * CATEGORY_AVG_SIZE) +
               (locations.Count * LOCATION_AVG_SIZE) +
               (departments.Count * DEPARTMENT_AVG_SIZE) +
               (deletedItems.Count * DELETED_ITEM_AVG_SIZE);
    }

    private AssetReadDTO MapAssetToDto(Asset a) => new AssetReadDTO
    {
        AssetId = a.AssetId,
        AssetTag = a.AssetTag,
        Name = a.Name,
        Description = a.Description,
        CategoryId = a.CategoryId,
        LocationId = a.LocationId,
        DepartmentId = a.DepartmentId,
        PurchaseDate = a.PurchaseDate,
        PurchasePrice = a.PurchasePrice,
        CurrentValue = a.CurrentValue,
        Status = a.Status,
        AssignedToUserId = a.AssignedToUserId,
        CreatedAt = a.CreatedAt,
        DateModified = a.DateModified,
        SerialNumber = a.SerialNumber,
        DigitalAssetTag = a.DigitalAssetTag,
        Condition = a.Condition,
        VendorName = a.VendorName,
        InvoiceNumber = a.InvoiceNumber,
        Quantity = a.Quantity,
        CostPerUnit = a.CostPerUnit,
        UsefulLifeYears = a.UsefulLifeYears,
        WarrantyExpiry = a.WarrantyExpiry,
        DisposalDate = a.DisposalDate,
        DisposalValue = a.DisposalValue,
        Remarks = a.Remarks,
        DepreciationRate = a.Category?.DepreciationRate,
        CalculatedUsefulLifeYears = a.CalculatedUsefulLifeYears,
        TotalCost = a.TotalCost,
        AccumulatedDepreciation = a.AccumulatedDepreciation,
        NetBookValue = a.NetBookValue,
        GainLossOnDisposal = a.GainLossOnDisposal
    };

    /// <summary>
    /// HIGH FIX #4: Helper method to detect field changes including null value changes
    /// Properly handles intentional field clears (setting to null)
    /// </summary>
    private static bool HasFieldChanged<T>(T? newValue, T? oldValue) where T : class
    {
        // Both null - no change
        if (newValue == null && oldValue == null) return false;
        
        // One is null, other isn't - this is a change (including intentional clears)
        if (newValue == null || oldValue == null) return true;
        
        // Both non-null - compare values
        return !newValue.Equals(oldValue);
    }
    
    /// <summary>
    /// HIGH FIX #4: Overload for nullable value types
    /// </summary>
    private static bool HasFieldChanged<T>(T? newValue, T? oldValue) where T : struct
    {
        // Both null - no change
        if (!newValue.HasValue && !oldValue.HasValue) return false;
        
        // One is null, other isn't - this is a change
        if (!newValue.HasValue || !oldValue.HasValue) return true;
        
        // Both have values - compare
        return !newValue.Value.Equals(oldValue.Value);
    }

}
