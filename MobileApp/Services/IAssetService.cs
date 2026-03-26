using Shared.Models;

namespace MobileApp.Services;

/// <summary>
/// Service for managing assets with offline-first CRUD operations
/// </summary>
public interface IAssetService
{
    /// <summary>
    /// Get all assets from local database
    /// </summary>
    Task<List<Asset>> GetAllAssetsAsync();

    /// <summary>
    /// Get a page of assets from local database for incremental loading
    /// </summary>
    Task<List<Asset>> GetAssetsPageAsync(int pageIndex, int pageSize);

    /// <summary>
    /// Get a single asset by ID
    /// </summary>
    Task<Asset?> GetAssetByIdAsync(string assetId);

    /// <summary>
    /// Create a new asset (works offline, syncs when online)
    /// </summary>
    Task<(bool Success, string Message)> CreateAssetAsync(Asset asset);

    /// <summary>
    /// Update an existing asset (works offline, syncs when online)
    /// </summary>
    Task<(bool Success, string Message)> UpdateAssetAsync(Asset asset);

    /// <summary>
    /// Upsert an asset - creates if new, updates if exists based on AssetTag or DigitalAssetTag
    /// </summary>
    Task<(bool Success, string Message, bool IsUpdate)> UpsertAssetAsync(Asset asset);

    /// <summary>
    /// Delete an asset (works offline, syncs when online)
    /// </summary>
    Task<(bool Success, string Message)> DeleteAssetAsync(string assetId);
}