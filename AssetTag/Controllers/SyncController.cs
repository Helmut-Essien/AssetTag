using AssetTag.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Shared.Models;
using System.Text.Json;

namespace AssetTag.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SyncController> _logger;

    public SyncController(ApplicationDbContext context, ILogger<SyncController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Push offline changes from mobile to server
    /// </summary>
    [HttpPost("push")]
    public async Task<ActionResult<SyncPushResponseDTO>> PushChanges([FromBody] SyncPushRequestDTO request)
    {
        var successCount = 0;
        var errors = new List<SyncErrorDTO>();

        _logger.LogInformation("Processing push sync from device {DeviceId} with {Count} operations", 
            request.DeviceId, request.Operations.Count);

        foreach (var operation in request.Operations.OrderBy(o => o.CreatedAt))
        {
            try
            {
                switch (operation.EntityType.ToLower())
                {
                    case "asset":
                        await ProcessAssetOperation(operation);
                        successCount++;
                        break;
                    
                    default:
                        errors.Add(new SyncErrorDTO
                        {
                            EntityId = operation.EntityId,
                            Operation = operation.Operation,
                            ErrorMessage = $"Unknown entity type: {operation.EntityType}"
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing sync operation for {EntityType} {EntityId}", 
                    operation.EntityType, operation.EntityId);
                
                errors.Add(new SyncErrorDTO
                {
                    EntityId = operation.EntityId,
                    Operation = operation.Operation,
                    ErrorMessage = ex.Message
                });
            }
        }

        _logger.LogInformation("Push sync completed: {SuccessCount} successful, {FailureCount} failed", 
            successCount, errors.Count);

        return Ok(new SyncPushResponseDTO
        {
            SuccessCount = successCount,
            FailureCount = errors.Count,
            Errors = errors
        });
    }

    /// <summary>
    /// Pull delta changes from server to mobile
    /// </summary>
    [HttpPost("pull")]
    public async Task<ActionResult<SyncPullResponseDTO>> PullChanges([FromBody] SyncPullRequestDTO request)
    {
        try
        {
            var lastSync = request.LastSyncTimestamp ?? DateTime.MinValue;

            _logger.LogInformation("Processing pull sync for device {DeviceId} since {LastSync}",
                request.DeviceId, lastSync);

            // Get all assets modified after last sync
            var assets = await _context.Assets
                .Include(a => a.Category)
                .Where(a => a.DateModified > lastSync)
                .ToListAsync();

            // Get categories that were modified OR are referenced by the assets being synced
            var modifiedCategories = await _context.Categories
                .Where(c => c.DateModified > lastSync)
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
                .Where(l => l.DateModified > lastSync)
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
                .Where(d => d.DateModified > lastSync)
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

            _logger.LogInformation(
                "Pull sync returning: {AssetCount} assets, {CategoryCount} categories, " +
                "{LocationCount} locations, {DepartmentCount} departments",
                assets.Count, categories.Count, locations.Count, departments.Count);

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
                ServerTimestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pull sync");
            return StatusCode(500, new { error = "Sync failed", details = ex.Message });
        }
    }

    private async Task ProcessAssetOperation(SyncOperationDTO operation)
    {
        switch (operation.Operation.ToUpper())
        {
            case "CREATE":
                var createDto = JsonSerializer.Deserialize<AssetCreateDTO>(operation.JsonData);
                if (createDto == null) throw new Exception("Invalid asset data");

                // Idempotency check - don't create if already exists
                if (await _context.Assets.AnyAsync(a => a.AssetId == operation.EntityId))
                {
                    _logger.LogInformation("Asset {AssetId} already exists, skipping create", operation.EntityId);
                    return;
                }

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
                    CreatedAt = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow
                };

                _context.Assets.Add(newAsset);
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
                    return;
                }

                // Apply updates (Last-Write-Wins conflict resolution)
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

                existingAsset.DateModified = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Updated asset {AssetId} via sync", operation.EntityId);
                break;

            case "DELETE":
                var assetToDelete = await _context.Assets.FindAsync(operation.EntityId);
                if (assetToDelete != null)
                {
                    _context.Assets.Remove(assetToDelete);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Deleted asset {AssetId} via sync", operation.EntityId);
                }
                break;
        }
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
}