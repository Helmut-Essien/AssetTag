using System.Text.Json;
using MobileApp.Tests.Helpers;
using MobileData.Data;
using Shared.DTOs;
using Shared.Models;
using Xunit;

namespace MobileApp.Tests.Data;

public sealed class LocalDbContextTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SaveChanges_CreateAsset_QueuesCreateOperation()
    {
        var category = TestEntities.Category();
        var location = TestEntities.Location();
        var department = TestEntities.Department();
        var asset = TestEntities.Asset(categoryId: category.CategoryId, locationId: location.LocationId, departmentId: department.DepartmentId);

        await using var context = _db.CreateContext();
        context.Categories.Add(category);
        context.Locations.Add(location);
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        context.Assets.Add(asset);
        await context.SaveChangesAsync();

        var queued = Assert.Single(context.SyncQueue);
        Assert.Equal("Asset", queued.EntityType);
        Assert.Equal(asset.AssetId, queued.EntityId);
        Assert.Equal("CREATE", queued.Operation);

        var dto = JsonSerializer.Deserialize<AssetCreateDTO>(queued.JsonData);
        Assert.NotNull(dto);
        Assert.Equal(asset.AssetTag, dto!.AssetTag);
        Assert.Equal(asset.Name, dto.Name);
    }

    [Fact]
    public async Task SaveChanges_UpdateAsset_QueuesPatchWithChangedFieldsOnly()
    {
        var category = TestEntities.Category();
        var location = TestEntities.Location();
        var department = TestEntities.Department();
        var asset = TestEntities.Asset(categoryId: category.CategoryId, locationId: location.LocationId, departmentId: department.DepartmentId);

        await using (var seed = _db.CreateContext())
        {
            seed.Categories.Add(category);
            seed.Locations.Add(location);
            seed.Departments.Add(department);
            seed.Assets.Add(asset);
            await seed.SaveChangesAsync();
        }

        await using var context = _db.CreateContext();
        var existing = await context.Assets.FindAsync(asset.AssetId);
        Assert.NotNull(existing);
        existing!.Name = "Updated Laptop";
        existing.Status = "In Use";
        await context.SaveChangesAsync();

        var patches = context.SyncQueue.Where(q => q.Operation == "PATCH").ToList();
        var patch = Assert.Single(patches);
        Assert.Equal(asset.AssetId, patch.EntityId);

        var dto = JsonSerializer.Deserialize<AssetPatchDTO>(patch.JsonData);
        Assert.NotNull(dto);
        Assert.True(dto!.Changes.ContainsKey(nameof(Asset.Name)));
        Assert.True(dto.Changes.ContainsKey(nameof(Asset.Status)));
        Assert.False(dto.Changes.ContainsKey(nameof(Asset.AssetTag)));
    }

    [Fact]
    public async Task SaveChanges_DeleteAsset_QueuesDeleteOperation()
    {
        var category = TestEntities.Category();
        var location = TestEntities.Location();
        var department = TestEntities.Department();
        var asset = TestEntities.Asset(categoryId: category.CategoryId, locationId: location.LocationId, departmentId: department.DepartmentId);

        await using (var seed = _db.CreateContext())
        {
            seed.Categories.Add(category);
            seed.Locations.Add(location);
            seed.Departments.Add(department);
            seed.Assets.Add(asset);
            await seed.SaveChangesAsync();
        }

        await using var context = _db.CreateContext();
        var existing = await context.Assets.FindAsync(asset.AssetId);
        context.Assets.Remove(existing!);
        await context.SaveChangesAsync();

        var deletes = context.SyncQueue.Where(q => q.Operation == "DELETE").ToList();
        var deleted = Assert.Single(deletes);
        Assert.Equal(asset.AssetId, deleted.EntityId);
        Assert.Contains(asset.AssetId, deleted.JsonData);
    }

    [Fact]
    public async Task SaveChanges_SuppressSyncQueue_DoesNotQueuePullUpdates()
    {
        var category = TestEntities.Category();
        var location = TestEntities.Location();
        var department = TestEntities.Department();
        var asset = TestEntities.Asset(categoryId: category.CategoryId, locationId: location.LocationId, departmentId: department.DepartmentId);

        await using var context = _db.CreateContext();
        context.SuppressSyncQueue = true;
        context.Categories.Add(category);
        context.Locations.Add(location);
        context.Departments.Add(department);
        context.Assets.Add(asset);
        await context.SaveChangesAsync();

        Assert.Empty(context.SyncQueue);
        Assert.Equal(1, context.Assets.Count());
    }

    [Fact]
    public async Task SaveChanges_UnchangedAsset_DoesNotQueuePatch()
    {
        var category = TestEntities.Category();
        var location = TestEntities.Location();
        var department = TestEntities.Department();
        var asset = TestEntities.Asset(categoryId: category.CategoryId, locationId: location.LocationId, departmentId: department.DepartmentId);

        await using (var seed = _db.CreateContext())
        {
            seed.Categories.Add(category);
            seed.Locations.Add(location);
            seed.Departments.Add(department);
            seed.Assets.Add(asset);
            await seed.SaveChangesAsync();
        }

        await using var context = _db.CreateContext();
        var existing = await context.Assets.FindAsync(asset.AssetId);
        existing!.DateModified = existing.DateModified; // no real change
        await context.SaveChangesAsync();

        Assert.DoesNotContain(context.SyncQueue, q => q.Operation == "PATCH");
    }

    [Fact]
    public async Task SaveChanges_CategoryChange_IsNotQueued()
    {
        await using var context = _db.CreateContext();
        context.Categories.Add(TestEntities.Category());
        await context.SaveChangesAsync();

        Assert.Empty(context.SyncQueue);
    }
}
