using Microsoft.Extensions.Logging.Abstractions;
using MobileApp.Services;
using MobileApp.Tests.Helpers;
using NSubstitute;
using Xunit;

namespace MobileApp.Tests.Services;

public sealed class AssetServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly ISyncService _sync = Substitute.For<ISyncService>();
    private readonly AssetService _sut;

    public AssetServiceTests()
    {
        _sync.EnqueuePushAsync().Returns(Task.FromResult((true, "queued")));
        _sut = new AssetService(_db.Services, _sync, NullLogger<AssetService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(string CategoryId, string LocationId, string DepartmentId)> SeedLookupsAsync()
    {
        var category = TestEntities.Category();
        var location = TestEntities.Location();
        var department = TestEntities.Department();
        await using var context = _db.CreateContext();
        context.SuppressSyncQueue = true;
        context.Categories.Add(category);
        context.Locations.Add(location);
        context.Departments.Add(department);
        await context.SaveChangesAsync();
        return (category.CategoryId, location.LocationId, department.DepartmentId);
    }

    [Fact]
    public async Task CreateAsset_PersistsAndEnqueuesPush()
    {
        var ids = await SeedLookupsAsync();
        var asset = TestEntities.Asset(categoryId: ids.CategoryId, locationId: ids.LocationId, departmentId: ids.DepartmentId);

        var (success, message) = await _sut.CreateAssetAsync(asset);

        Assert.True(success);
        Assert.Contains("created", message, StringComparison.OrdinalIgnoreCase);

        var loaded = await _sut.GetAssetByIdAsync(asset.AssetId);
        Assert.NotNull(loaded);
        Assert.Equal(asset.AssetTag, loaded!.AssetTag);
        await _sync.Received().EnqueuePushAsync();
    }

    [Fact]
    public async Task CreateAsset_GeneratesUlidWhenMissing()
    {
        var ids = await SeedLookupsAsync();
        var asset = TestEntities.Asset(id: "", categoryId: ids.CategoryId, locationId: ids.LocationId, departmentId: ids.DepartmentId);

        var (success, _) = await _sut.CreateAssetAsync(asset);

        Assert.True(success);
        Assert.False(string.IsNullOrEmpty(asset.AssetId));
    }

    [Fact]
    public async Task UpdateAsset_ChangesNameAndQueuesSync()
    {
        var ids = await SeedLookupsAsync();
        var asset = TestEntities.Asset(categoryId: ids.CategoryId, locationId: ids.LocationId, departmentId: ids.DepartmentId);
        await _sut.CreateAssetAsync(asset);

        asset.Name = "Desktop";
        var (success, _) = await _sut.UpdateAssetAsync(asset);

        Assert.True(success);
        var loaded = await _sut.GetAssetByIdAsync(asset.AssetId);
        Assert.Equal("Desktop", loaded!.Name);
    }

    [Fact]
    public async Task UpdateAsset_MissingId_ReturnsNotFound()
    {
        var (success, message) = await _sut.UpdateAssetAsync(TestEntities.Asset());

        Assert.False(success);
        Assert.Contains("not found", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpsertAsset_CreatesWhenTagIsNew()
    {
        var ids = await SeedLookupsAsync();
        var asset = TestEntities.Asset(categoryId: ids.CategoryId, locationId: ids.LocationId, departmentId: ids.DepartmentId);

        var (success, _, isUpdate) = await _sut.UpsertAssetAsync(asset);

        Assert.True(success);
        Assert.False(isUpdate);
    }

    [Fact]
    public async Task UpsertAsset_UpdatesWhenAssetTagMatches()
    {
        var ids = await SeedLookupsAsync();
        var asset = TestEntities.Asset(tag: "TAG-100", categoryId: ids.CategoryId, locationId: ids.LocationId, departmentId: ids.DepartmentId);
        await _sut.CreateAssetAsync(asset);

        var replacement = TestEntities.Asset(
            tag: "TAG-100",
            name: "Replaced",
            categoryId: ids.CategoryId,
            locationId: ids.LocationId,
            departmentId: ids.DepartmentId);

        var (success, _, isUpdate) = await _sut.UpsertAssetAsync(replacement);

        Assert.True(success);
        Assert.True(isUpdate);
        var loaded = await _sut.GetAssetByIdAsync(asset.AssetId);
        Assert.Equal("Replaced", loaded!.Name);
        Assert.Equal(asset.AssetId, loaded.AssetId);
    }

    [Fact]
    public async Task UpsertAsset_MatchesDigitalAssetTagWhenTagMissing()
    {
        var ids = await SeedLookupsAsync();
        var asset = TestEntities.Asset(
            tag: "TAG-200",
            digitalTag: "DIG-9",
            categoryId: ids.CategoryId,
            locationId: ids.LocationId,
            departmentId: ids.DepartmentId);
        await _sut.CreateAssetAsync(asset);

        var replacement = TestEntities.Asset(
            tag: "TAG-NEW",
            digitalTag: "DIG-9",
            name: "From digital tag",
            categoryId: ids.CategoryId,
            locationId: ids.LocationId,
            departmentId: ids.DepartmentId);

        var (_, _, isUpdate) = await _sut.UpsertAssetAsync(replacement);

        Assert.True(isUpdate);
        var loaded = await _sut.GetAssetByIdAsync(asset.AssetId);
        Assert.Equal("From digital tag", loaded!.Name);
        Assert.Equal("TAG-NEW", loaded.AssetTag);
    }

    [Fact]
    public async Task DeleteAsset_RemovesRow()
    {
        var ids = await SeedLookupsAsync();
        var asset = TestEntities.Asset(categoryId: ids.CategoryId, locationId: ids.LocationId, departmentId: ids.DepartmentId);
        await _sut.CreateAssetAsync(asset);

        var (success, _) = await _sut.DeleteAssetAsync(asset.AssetId);

        Assert.True(success);
        Assert.Null(await _sut.GetAssetByIdAsync(asset.AssetId));
    }

    [Fact]
    public async Task GetAssetsPage_FiltersBySearchAndCategory()
    {
        var ids = await SeedLookupsAsync();
        await _sut.CreateAssetAsync(TestEntities.Asset(tag: "AAA-1", name: "Alpha Chair", categoryId: ids.CategoryId, locationId: ids.LocationId, departmentId: ids.DepartmentId));
        await _sut.CreateAssetAsync(TestEntities.Asset(tag: "BBB-1", name: "Beta Desk", categoryId: ids.CategoryId, locationId: ids.LocationId, departmentId: ids.DepartmentId));

        var page = await _sut.GetAssetsPageAsync(0, 10, searchText: "Chair", categoryName: "IT Equipment");

        var match = Assert.Single(page);
        Assert.Equal("Alpha Chair", match.Name);
    }

    [Fact]
    public async Task GetAssetsPage_PendingSyncOnly_UsesProvidedIds()
    {
        var ids = await SeedLookupsAsync();
        var pending = TestEntities.Asset(name: "Pending", categoryId: ids.CategoryId, locationId: ids.LocationId, departmentId: ids.DepartmentId);
        var synced = TestEntities.Asset(name: "Synced", categoryId: ids.CategoryId, locationId: ids.LocationId, departmentId: ids.DepartmentId);
        await _sut.CreateAssetAsync(pending);
        await _sut.CreateAssetAsync(synced);

        var page = await _sut.GetAssetsPageAsync(
            0, 10,
            pendingSyncOnly: true,
            pendingSyncIds: new[] { pending.AssetId });

        var match = Assert.Single(page);
        Assert.Equal(pending.AssetId, match.AssetId);
    }

    [Fact]
    public async Task GetAssetsPage_SortsNameDescending()
    {
        var ids = await SeedLookupsAsync();
        await _sut.CreateAssetAsync(TestEntities.Asset(name: "Alpha", categoryId: ids.CategoryId, locationId: ids.LocationId, departmentId: ids.DepartmentId));
        await _sut.CreateAssetAsync(TestEntities.Asset(name: "Zulu", categoryId: ids.CategoryId, locationId: ids.LocationId, departmentId: ids.DepartmentId));

        var page = await _sut.GetAssetsPageAsync(0, 10, sortOption: "Name (Z-A)");

        Assert.Equal("Zulu", page[0].Name);
        Assert.Equal("Alpha", page[1].Name);
    }

    [Fact]
    public async Task GetCategoryNames_ReturnsSortedDistinctNames()
    {
        await using var context = _db.CreateContext();
        context.Categories.Add(TestEntities.Category(name: "Vehicles"));
        context.Categories.Add(TestEntities.Category(name: "Furniture"));
        await context.SaveChangesAsync();

        var names = await _sut.GetCategoryNamesAsync();

        Assert.Equal(new[] { "Furniture", "Vehicles" }, names);
    }
}
