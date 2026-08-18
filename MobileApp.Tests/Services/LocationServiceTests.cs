using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MobileApp.Services;
using MobileApp.Tests.Helpers;
using NSubstitute;
using Shared.DTOs;
using Xunit;

namespace MobileApp.Tests.Services;

public sealed class LocationServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly TestHttpMessageHandler _http = new();
    private readonly LocationService _sut;

    public LocationServiceTests()
    {
        _auth.IsConnectedToInternet().Returns(true);
        _sut = new LocationService(
            _db.Services,
            ServiceTestFactory.HttpFactory(_http),
            _auth,
            NullLogger<LocationService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAllLocations_ReturnsSortedByName()
    {
        await using var context = _db.CreateContext();
        context.Locations.Add(TestEntities.Location(name: "Zebra", campus: "A"));
        context.Locations.Add(TestEntities.Location(name: "Alpha", campus: "B"));
        await context.SaveChangesAsync();

        var locations = await _sut.GetAllLocationsAsync();

        Assert.Equal(new[] { "Alpha", "Zebra" }, locations.Select(l => l.Name).ToArray());
    }

    [Fact]
    public async Task GetLocationsPage_FiltersByCampusAndBuilding()
    {
        await using var context = _db.CreateContext();
        context.Locations.Add(TestEntities.Location(name: "Lab 1", campus: "North"));
        var south = TestEntities.Location(name: "Store", campus: "South");
        south.Building = "Warehouse";
        context.Locations.Add(south);
        await context.SaveChangesAsync();

        var page = await _sut.GetLocationsPageAsync(0, 10, searchText: "Warehouse");

        var match = Assert.Single(page);
        Assert.Equal("Store", match.Name);
    }

    [Fact]
    public async Task CreateLocation_RequiresInternet()
    {
        _auth.IsConnectedToInternet().Returns(false);

        var (success, message, location) = await _sut.CreateLocationAsync(TestEntities.Location());

        Assert.False(success);
        Assert.Null(location);
        Assert.Contains("internet", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateLocation_SavesApiResponseLocallyWithoutSyncQueue()
    {
        var created = new LocationReadDTO("loc-1", "New Lab", "Desc", "North", "A", "12", 5.6, -0.2);
        _http.RespondJson("api/locations", HttpStatusCode.OK, created);

        var (success, _, location) = await _sut.CreateLocationAsync(TestEntities.Location(name: "Ignored"));

        Assert.True(success);
        Assert.Equal("loc-1", location!.LocationId);
        Assert.Equal("New Lab", location.Name);

        await using var context = _db.CreateContext();
        Assert.NotNull(await context.Locations.FindAsync("loc-1"));
        Assert.Empty(context.SyncQueue);
    }

    [Fact]
    public async Task GetLocationNames_ReturnsDistinctSortedNames()
    {
        await using var context = _db.CreateContext();
        context.Locations.Add(TestEntities.Location(name: "Lab", campus: "A"));
        context.Locations.Add(TestEntities.Location(name: "Office", campus: "B"));
        await context.SaveChangesAsync();

        var names = await _sut.GetLocationNamesAsync();

        Assert.Equal(new[] { "Lab", "Office" }, names);
    }
}
