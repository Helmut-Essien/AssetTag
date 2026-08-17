using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MobileApp.Services;
using MobileApp.Tests.Helpers;
using MobileData.Data;
using NSubstitute;
using Shared.DTOs;
using Shared.Models;
using SharedLocation = Shared.Models.Location;
using Xunit;

namespace MobileApp.Tests.Services;

public sealed class SyncServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly TestHttpMessageHandler _http = new();
    private readonly SyncService _sut;

    public SyncServiceTests()
    {
        _auth.IsConnectedToInternet().Returns(true);
        _sut = new SyncService(
            _db.Services,
            ServiceTestFactory.HttpFactory(_http),
            _auth,
            NullLogger<SyncService>.Instance);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _db.Dispose();
    }

    private async Task<(Category Category, SharedLocation Location, Department Department)> SeedReferenceDataAsync()
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
        return (category, location, department);
    }

    [Fact]
    public async Task PushChanges_NoInternet_ReturnsFailureWithoutCallingApi()
    {
        _auth.IsConnectedToInternet().Returns(false);

        var (success, message) = await _sut.PushChangesAsync();

        Assert.False(success);
        Assert.Contains("internet", message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_http.Requests);
    }

    [Fact]
    public async Task PushChanges_EmptyQueue_SucceedsWithoutHttp()
    {
        var (success, message) = await _sut.PushChangesAsync();

        Assert.True(success);
        Assert.Contains("No changes", message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_http.Requests);
    }

    [Fact]
    public async Task PushChanges_RemovesSuccessfulQueueItems()
    {
        var (category, location, department) = await SeedReferenceDataAsync();
        var asset = TestEntities.Asset(categoryId: category.CategoryId, locationId: location.LocationId, departmentId: department.DepartmentId);

        int queueId;
        await using (var context = _db.CreateContext())
        {
            context.Assets.Add(asset);
            await context.SaveChangesAsync();
            queueId = context.SyncQueue.Single().Id;
        }

        _http.RespondJson("api/sync/push", HttpStatusCode.OK, new SyncPushResponseDTO
        {
            SuccessCount = 1,
            FailureCount = 0,
            SuccessfulOperationIds = new List<int> { queueId }
        });

        var (success, _) = await _sut.PushChangesAsync();

        Assert.True(success);
        await using var verify = _db.CreateContext();
        Assert.Empty(verify.SyncQueue);
    }

    [Fact]
    public async Task PushChanges_FailedItem_IncrementsRetryCount()
    {
        var (category, location, department) = await SeedReferenceDataAsync();
        var asset = TestEntities.Asset(categoryId: category.CategoryId, locationId: location.LocationId, departmentId: department.DepartmentId);

        await using (var context = _db.CreateContext())
        {
            context.Assets.Add(asset);
            await context.SaveChangesAsync();
        }

        _http.RespondJson("api/sync/push", HttpStatusCode.OK, new SyncPushResponseDTO
        {
            SuccessCount = 0,
            FailureCount = 1,
            SuccessfulOperationIds = new List<int>(),
            Errors = new List<SyncErrorDTO>
            {
                new() { EntityId = asset.AssetId, Operation = "CREATE", ErrorMessage = "Validation failed" }
            }
        });

        var (success, _) = await _sut.PushChangesAsync();

        Assert.True(success);
        await using var verify = _db.CreateContext();
        var item = Assert.Single(verify.SyncQueue);
        Assert.Equal(1, item.RetryCount);
    }

    [Fact]
    public async Task PushChanges_InfrastructureFailure_DoesNotIncrementRetry()
    {
        var (category, location, department) = await SeedReferenceDataAsync();
        var asset = TestEntities.Asset(categoryId: category.CategoryId, locationId: location.LocationId, departmentId: department.DepartmentId);

        await using (var context = _db.CreateContext())
        {
            context.Assets.Add(asset);
            await context.SaveChangesAsync();
        }

        _http.Respond("api/sync/push", HttpStatusCode.ServiceUnavailable, "down");

        var (success, message) = await _sut.PushChangesAsync();

        Assert.False(success);
        Assert.Contains("infrastructure", message, StringComparison.OrdinalIgnoreCase);
        await using var verify = _db.CreateContext();
        Assert.Equal(0, verify.SyncQueue.Single().RetryCount);
    }

    [Fact]
    public async Task PullChanges_InsertsReferenceDataAndAssetsWithoutQueueing()
    {
        var category = TestEntities.Category();
        var location = TestEntities.Location();
        var department = TestEntities.Department();
        var asset = TestEntities.Asset(categoryId: category.CategoryId, locationId: location.LocationId, departmentId: department.DepartmentId);

        _http.RespondJson("api/sync/pull", HttpStatusCode.OK, new SyncPullResponseDTO
        {
            Categories = new List<CategoryReadDTO>
            {
                new(category.CategoryId, category.Name, category.Description, category.DepreciationRate)
            },
            Locations = new List<LocationReadDTO>
            {
                new(location.LocationId, location.Name, location.Description, location.Campus, location.Building, location.Room, location.Latitude, location.Longitude)
            },
            Departments = new List<DepartmentReadDTO>
            {
                new(department.DepartmentId, department.Name, department.Description)
            },
            Assets = new List<AssetReadDTO> { TestEntities.AssetReadDto(asset) },
            ServerTimestamp = DateTime.UtcNow
        });

        var (success, _) = await _sut.PullChangesAsync();

        Assert.True(success);
        await using var verify = _db.CreateContext();
        Assert.NotNull(await verify.Assets.FindAsync(asset.AssetId));
        Assert.Empty(verify.SyncQueue);
        Assert.NotEqual(default, verify.DeviceInfo.Single().LastSync);
    }

    [Fact]
    public async Task PullChanges_SkipsAssetWithPendingLocalChanges()
    {
        var (category, location, department) = await SeedReferenceDataAsync();
        var local = TestEntities.Asset(name: "Local Name", categoryId: category.CategoryId, locationId: location.LocationId, departmentId: department.DepartmentId);

        await using (var context = _db.CreateContext())
        {
            context.Assets.Add(local);
            await context.SaveChangesAsync();
        }

        var serverCopy = TestEntities.AssetReadDto(local) with { Name = "Server Name" };
        _http.RespondJson("api/sync/pull", HttpStatusCode.OK, new SyncPullResponseDTO
        {
            Categories = new List<CategoryReadDTO>
            {
                new(category.CategoryId, category.Name, null, category.DepreciationRate)
            },
            Locations = new List<LocationReadDTO>
            {
                new(location.LocationId, location.Name, null, location.Campus, location.Building, location.Room, null, null)
            },
            Departments = new List<DepartmentReadDTO>
            {
                new(department.DepartmentId, department.Name, null)
            },
            Assets = new List<AssetReadDTO> { serverCopy },
            ServerTimestamp = DateTime.UtcNow
        });

        await _sut.PullChangesAsync();

        await using var verify = _db.CreateContext();
        Assert.Equal("Local Name", (await verify.Assets.FindAsync(local.AssetId))!.Name);
    }

    [Fact]
    public async Task PullChanges_MissingReferences_RecordsSkippedAsset()
    {
        var orphan = TestEntities.Asset();
        _http.RespondJson("api/sync/pull", HttpStatusCode.OK, new SyncPullResponseDTO
        {
            Assets = new List<AssetReadDTO> { TestEntities.AssetReadDto(orphan) },
            ServerTimestamp = DateTime.UtcNow
        });

        await _sut.PullChangesAsync();

        await using var verify = _db.CreateContext();
        Assert.Null(await verify.Assets.FindAsync(orphan.AssetId));
        var skipped = Assert.Single(verify.SkippedAssets);
        Assert.Equal(orphan.AssetId, skipped.AssetId);
    }

    [Fact]
    public async Task GetPendingSyncCount_CountsQueueItems()
    {
        var (category, location, department) = await SeedReferenceDataAsync();
        await using (var context = _db.CreateContext())
        {
            context.Assets.Add(TestEntities.Asset(categoryId: category.CategoryId, locationId: location.LocationId, departmentId: department.DepartmentId));
            await context.SaveChangesAsync();
        }

        Assert.Equal(1, await _sut.GetPendingSyncCountAsync());
    }

    [Fact]
    public async Task ClearAllLocalData_WipesEntitiesAndQueue()
    {
        var (category, location, department) = await SeedReferenceDataAsync();
        await using (var context = _db.CreateContext())
        {
            context.Assets.Add(TestEntities.Asset(categoryId: category.CategoryId, locationId: location.LocationId, departmentId: department.DepartmentId));
            await context.SaveChangesAsync();
        }

        await _sut.ClearAllLocalDataAsync();

        await using var verify = _db.CreateContext();
        Assert.Empty(verify.Assets);
        Assert.Empty(verify.Categories);
        Assert.Empty(verify.SyncQueue);
    }

    [Fact]
    public async Task ResetSyncState_SetsLastSyncToEpoch()
    {
        await using (var context = _db.CreateContext())
        {
            context.DeviceInfo.Add(new MobileData.Data.DeviceInfo
            {
                DeviceId = "device-1",
                LastSync = DateTime.UtcNow,
                SyncToken = string.Empty
            });
            await context.SaveChangesAsync();
        }

        await _sut.ResetSyncStateAsync();

        await using var verify = _db.CreateContext();
        Assert.Equal(new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), verify.DeviceInfo.Single().LastSync);
    }

    [Fact]
    public async Task ConcurrentPush_SecondCallIsRejected()
    {
        _auth.IsConnectedToInternet().Returns(async _ =>
        {
            await Task.Delay(200);
            return true;
        });

        var first = _sut.PushChangesAsync();
        var second = _sut.PushChangesAsync();
        var results = await Task.WhenAll(first, second);

        Assert.Contains(results, r => r.Message.Contains("already in progress", StringComparison.OrdinalIgnoreCase));
    }
}
