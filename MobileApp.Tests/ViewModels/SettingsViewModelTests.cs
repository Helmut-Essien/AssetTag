using MobileApp.Services;
using MobileApp.ViewModels;
using NSubstitute;
using Xunit;

namespace MobileApp.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    private static SettingsViewModel Create(
        out IAuthService auth,
        out ISyncService sync,
        out INavigationService navigation,
        out IVersionCheckService versions)
    {
        auth = Substitute.For<IAuthService>();
        sync = Substitute.For<ISyncService>();
        navigation = Substitute.For<INavigationService>();
        versions = Substitute.For<IVersionCheckService>();
        versions.GetCurrentVersion().Returns("1.0.2");
        versions.IsBetaUpdatesEnabled.Returns(false);
        return new SettingsViewModel(auth, sync, navigation, versions);
    }

    [Fact]
    public void Constructor_SetsProductionChannelLabel()
    {
        var vm = Create(out _, out _, out _, out _);

        Assert.Equal("Production", vm.UpdateChannelLabel);
        Assert.Contains("1.0.2", vm.AppVersion);
    }

    [Fact]
    public void EnablingBetaUpdates_UpdatesChannelLabel()
    {
        var vm = Create(out _, out _, out _, out var versions);

        vm.BetaUpdatesEnabled = true;

        versions.Received().SetBetaUpdatesEnabled(true);
        Assert.Equal("Beta", vm.UpdateChannelLabel);
        Assert.Contains("Pre-releases", vm.BetaUpdatesStatusText);
    }

    [Fact]
    public async Task Logout_Cancelled_DoesNotWipeData()
    {
        var vm = Create(out var auth, out var sync, out var navigation, out _);
        navigation.DisplayConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        await vm.LogoutCommand.ExecuteAsync(null);

        await sync.DidNotReceive().ClearAllLocalDataAsync();
        await auth.DidNotReceive().LogoutAsync();
        await navigation.DidNotReceive().ShowLoginAsync();
    }

    [Fact]
    public async Task Logout_WipeFailure_AbortsAndKeepsSession()
    {
        var vm = Create(out var auth, out var sync, out var navigation, out _);
        navigation.DisplayConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);
        sync.ClearAllLocalDataAsync().Returns<Task>(_ => throw new InvalidOperationException("disk full"));

        await vm.LogoutCommand.ExecuteAsync(null);

        await auth.DidNotReceive().LogoutAsync();
        await navigation.DidNotReceive().ShowLoginAsync();
        await navigation.Received().DisplayAlertAsync(
            "Logout Failed",
            Arg.Is<string>(m => m.Contains("still logged in")),
            "OK");
    }

    [Fact]
    public async Task Logout_Success_WipesThenShowsLogin()
    {
        var vm = Create(out var auth, out var sync, out var navigation, out _);
        navigation.DisplayConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);
        auth.LogoutAsync().Returns((true, "Logged out successfully"));

        await vm.LogoutCommand.ExecuteAsync(null);

        Received.InOrder(() =>
        {
            sync.ClearAllLocalDataAsync();
            auth.LogoutAsync();
            navigation.ShowLoginAsync();
        });
    }
}
