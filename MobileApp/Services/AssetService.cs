using MobileData.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Models;
using NUlid;

namespace MobileApp.Services;

public class AssetService : IAssetService
{
    private readonly LocalDbContext _dbContext;
    private readonly ISyncService _syncService;
    private readonly ILogger<AssetService> _logger;

    public AssetService(
        LocalDbContext dbContext, 
        ISyncService syncService,
        ILogger<AssetService> logger)
    {
        _dbContext = dbContext;
        _syncService = syncService;
        _logger = logger;
    }

    public async Task<List<Asset>> GetAllAssetsAsync()
    {
        try
        {
            return await _dbContext.Assets
                .Include(a => a.Category)
                .Include(a => a.Location)
                .Include(a => a.Department)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all assets");
            return new List<Asset>();
        }
    }

    public async Task<Asset?> GetAssetByIdAsync(string assetId)
    {
        try
        {
            return await _dbContext.Assets
                .Include(a => a.Category)
                .Include(a => a.Location)
                .Include(a => a.Department)
                .FirstOrDefaultAsync(a => a.AssetId == assetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset {AssetId}", assetId);
            return null;
        }
    }

    public async Task<(bool Success, string Message)> CreateAssetAsync(Asset asset)
    {
        try
        {
            // Generate ULID if not provided
            if (string.IsNullOrEmpty(asset.AssetId))
            {
                asset.AssetId = Ulid.NewUlid().ToString();
            }

            asset.CreatedAt = DateTime.UtcNow;
            asset.DateModified = DateTime.UtcNow;

            _dbContext.Assets.Add(asset);
            await _dbContext.SaveChangesAsync(); // Automatically queues to SyncQueue

            _logger.LogInformation("Created asset {AssetId} ({AssetTag}) offline", 
                asset.AssetId, asset.AssetTag);

            // Try to sync immediately if online (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _syncService.PushChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background sync failed after create");
                }
            });

            return (true, "Asset created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating asset");
            return (false, $"Error creating asset: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> UpdateAssetAsync(Asset asset)
    {
        try
        {
            var existing = await _dbContext.Assets.FindAsync(asset.AssetId);
            if (existing == null)
            {
                _logger.LogWarning("Asset {AssetId} not found for update", asset.AssetId);
                return (false, "Asset not found");
            }

            // Update all fields
            existing.AssetTag = asset.AssetTag;
            existing.Name = asset.Name;
            existing.Description = asset.Description;
            existing.CategoryId = asset.CategoryId;
            existing.LocationId = asset.LocationId;
            existing.DepartmentId = asset.DepartmentId;
            existing.PurchaseDate = asset.PurchaseDate;
            existing.PurchasePrice = asset.PurchasePrice;
            existing.CurrentValue = asset.CurrentValue;
            existing.Status = asset.Status;
            existing.AssignedToUserId = asset.AssignedToUserId;
            existing.SerialNumber = asset.SerialNumber;
            existing.DigitalAssetTag = asset.DigitalAssetTag;
            existing.Condition = asset.Condition;
            existing.VendorName = asset.VendorName;
            existing.InvoiceNumber = asset.InvoiceNumber;
            existing.Quantity = asset.Quantity;
            existing.CostPerUnit = asset.CostPerUnit;
            existing.UsefulLifeYears = asset.UsefulLifeYears;
            existing.WarrantyExpiry = asset.WarrantyExpiry;
            existing.DisposalDate = asset.DisposalDate;
            existing.DisposalValue = asset.DisposalValue;
            existing.Remarks = asset.Remarks;
            existing.DateModified = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(); // Automatically queues to SyncQueue

            _logger.LogInformation("Updated asset {AssetId} ({AssetTag}) offline", 
                asset.AssetId, asset.AssetTag);

            // Try to sync immediately if online (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _syncService.PushChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background sync failed after update");
                }
            });

            return (true, "Asset updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating asset {AssetId}", asset.AssetId);
            return (false, $"Error updating asset: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DeleteAssetAsync(string assetId)
    {
        try
        {
            var asset = await _dbContext.Assets.FindAsync(assetId);
            if (asset == null)
            {
                _logger.LogWarning("Asset {AssetId} not found for deletion", assetId);
                return (false, "Asset not found");
            }

            var assetTag = asset.AssetTag;
            _dbContext.Assets.Remove(asset);
            await _dbContext.SaveChangesAsync(); // Automatically queues to SyncQueue

            _logger.LogInformation("Deleted asset {AssetId} ({AssetTag}) offline", 
                assetId, assetTag);

            // Try to sync immediately if online (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _syncService.PushChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background sync failed after delete");
                }
            });

            return (true, "Asset deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting asset {AssetId}", assetId);
            return (false, $"Error deleting asset: {ex.Message}");
        }
    }
}