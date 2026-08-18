using MobileApp.Services;
using MobileApp.Tests.Helpers;
using MobileApp.ViewModels;
using NSubstitute;
using Xunit;

namespace MobileApp.Tests.ViewModels;

public sealed class InventoryViewModelTests
{
    [Fact]
    public void Constructor_StartsBusyForSkeleton()
    {
        var vm = Create();
        Assert.True(vm.IsBusy);
        Assert.True(vm.IsInitialLoad);
        Assert.Equal("Inventory", vm.Title);
    }

    [Fact]
    public void DefaultFilters_AreUnrestricted()
    {
        var vm = Create();
        Assert.True(vm.IsAllFilterActive);
        Assert.Equal("All Categories", vm.SelectedCategory);
        Assert.Equal("All Locations", vm.SelectedLocation);
        Assert.Equal("Name (A-Z)", vm.CurrentSortOption);
    }

    private static InventoryViewModel Create()
    {
        using var db = new SqliteTestDatabase();
        return new InventoryViewModel(
            db.Services,
            Substitute.For<IAuthService>(),
            Substitute.For<IAssetService>(),
            Substitute.For<ILocationService>(),
            Substitute.For<ISyncService>(),
            Substitute.For<IBarcodeScannerService>());
    }
}
