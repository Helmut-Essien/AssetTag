using NUlid;
using Shared.Constants;
using Shared.DTOs;
using Shared.Models;
using SharedLocation = Shared.Models.Location;

namespace MobileApp.Tests.Helpers;

public static class TestEntities
{
    public static Category Category(string? id = null, string name = "IT Equipment", decimal? rate = 20m) => new()
    {
        CategoryId = id ?? Ulid.NewUlid().ToString(),
        Name = name,
        DepreciationRate = rate,
        DateModified = DateTime.UtcNow
    };

    public static SharedLocation Location(string? id = null, string name = "Main Lab", string campus = "North") => new()
    {
        LocationId = id ?? Ulid.NewUlid().ToString(),
        Name = name,
        Campus = campus,
        Building = "A",
        Room = "101",
        DateModified = DateTime.UtcNow,
        Assets = new List<Asset>()
    };

    public static Department Department(string? id = null, string name = "Finance") => new()
    {
        DepartmentId = id ?? Ulid.NewUlid().ToString(),
        Name = name,
        DateModified = DateTime.UtcNow,
        Users = new List<ApplicationUser>()
    };

    public static Asset Asset(
        string? id = null,
        string? tag = null,
        string name = "Laptop",
        string? categoryId = null,
        string? locationId = null,
        string? departmentId = null,
        string? digitalTag = null)
    {
        return new Asset
        {
            AssetId = id ?? Ulid.NewUlid().ToString(),
            AssetTag = tag ?? $"TAG-{Ulid.NewUlid()}",
            Name = name,
            CategoryId = categoryId ?? Ulid.NewUlid().ToString(),
            LocationId = locationId ?? Ulid.NewUlid().ToString(),
            DepartmentId = departmentId ?? Ulid.NewUlid().ToString(),
            Status = AssetConstants.Status.Available,
            Condition = AssetConstants.Condition.Good,
            DigitalAssetTag = digitalTag,
            Quantity = 1
        };
    }

    public static AssetReadDTO AssetReadDto(Asset asset) => new()
    {
        AssetId = asset.AssetId,
        AssetTag = asset.AssetTag,
        Name = asset.Name,
        Description = asset.Description,
        CategoryId = asset.CategoryId,
        LocationId = asset.LocationId,
        DepartmentId = asset.DepartmentId,
        Status = asset.Status,
        Condition = asset.Condition,
        DigitalAssetTag = asset.DigitalAssetTag,
        Quantity = asset.Quantity,
        CreatedAt = asset.CreatedAt,
        DateModified = asset.DateModified
    };
}
