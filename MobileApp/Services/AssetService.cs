using MobileData.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Models;
using NUlid;

namespace MobileApp.Services;

public class AssetService : IAssetService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISyncService _syncService;
    private readonly ILogger<AssetService> _logger;

    public AssetService(
        IServiceProvider serviceProvider,
        ISyncService syncService,
        ILogger<AssetService> logger)
    {
        _serviceProvider = serviceProvider;
        _syncService = syncService;
        _logger = logger;
    }

    public async Task<List<Asset>> GetAllAssetsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            return await dbContext.Assets
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

    public async Task<List<Asset>>GetAssetsPageAsync(int pageIndex, int pageSize)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            return await dbContext.Assets
                .AsNoTracking()
                .Include(a => a.Category)
                .Include(a => a.Location)
                .Include(a => a.Department)
                .OrderBy(a => a.Name)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assets page {PageIndex} size {PageSize}", pageIndex, pageSize);
            return new List<Asset>();
        }
    }

    public async Task<Asset?> GetAssetByIdAsync(string assetId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            return await dbContext.Assets
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

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            dbContext.Assets.Add(asset);
            await dbContext.SaveChangesAsync(); // Automatically queues to SyncQueue

            _logger.LogInformation("Created asset {AssetId} ({AssetTag}) offline", 
                asset.AssetId, asset.AssetTag);

            // Enqueue a push sync request (fire-and-forget)
            _ = _syncService.EnqueuePushAsync();

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
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            var existing = await dbContext.Assets.FindAsync(asset.AssetId);
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

            await dbContext.SaveChangesAsync(); // Automatically queues to SyncQueue

            _logger.LogInformation("Updated asset {AssetId} ({AssetTag}) offline", 
                asset.AssetId, asset.AssetTag);

            // Enqueue a push sync request (fire-and-forget)
            _ = _syncService.EnqueuePushAsync();

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
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

            var asset = await dbContext.Assets.FindAsync(assetId);
            if (asset == null)
            {
                _logger.LogWarning("Asset {AssetId} not found for deletion", assetId);
                return (false, "Asset not found");
            }

            var assetTag = asset.AssetTag;
            dbContext.Assets.Remove(asset);
            await dbContext.SaveChangesAsync(); // Automatically queues to SyncQueue

            _logger.LogInformation("Deleted asset {AssetId} ({AssetTag}) offline", 
                assetId, assetTag);

            // Enqueue a push sync request (fire-and-forget)
            _ = _syncService.EnqueuePushAsync();

            return (true, "Asset deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting asset {AssetId}", assetId);
            return (false, $"Error deleting asset: {ex.Message}");
        }
    }
}