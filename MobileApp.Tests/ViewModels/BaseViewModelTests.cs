using MobileApp.Services;
using MobileApp.ViewModels;
using NSubstitute;
using Shared.DTOs;
using Xunit;

namespace MobileApp.Tests.ViewModels;

public sealed class TestableBaseViewModel : BaseViewModel
{
    public Task<bool> ValidateAsync(IAuthService auth) => ValidateTokenAsync(auth);

    public Task<bool> ValidateSilentAsync(IAuthService auth) => TryValidateTokenSilentAsync(auth);
}

public sealed class BaseViewModelTests
{
    [Fact]
    public void IsNotBusy_TracksIsBusy()
    {
        var vm = new TestableBaseViewModel { IsBusy = true };
        Assert.False(vm.IsNotBusy);

        vm.IsBusy = false;
        Assert.True(vm.IsNotBusy);
    }

    [Fact]
    public async Task ValidateToken_MissingTokens_ReturnsFalse()
    {
        var auth = Substitute.For<IAuthService>();
        auth.GetStoredTokensAsync().Returns(((string?)null, (string?)null));
        var vm = new TestableBaseViewModel();

        Assert.False(await vm.ValidateAsync(auth));
    }

    [Fact]
    public async Task ValidateToken_ValidAccessToken_ReturnsTrue()
    {
        var auth = Substitute.For<IAuthService>();
        auth.GetStoredTokensAsync().Returns(("access", "refresh"));
        auth.IsTokenExpiredAsync().Returns(false);
        var vm = new TestableBaseViewModel();

        Assert.True(await vm.ValidateAsync(auth));
        await auth.DidNotReceive().RefreshTokenAsync();
    }

    [Fact]
    public async Task ValidateToken_TransientRefreshFailure_ReturnsFalseWithoutTreatingAsLogout()
    {
        var auth = Substitute.For<IAuthService>();
        auth.GetStoredTokensAsync().Returns(("access", "refresh"));
        auth.IsTokenExpiredAsync().Returns(true);
        auth.RefreshTokenAsync().Returns(TokenRefreshResult.Transient("offline"));
        var vm = new TestableBaseViewModel();

        Assert.False(await vm.ValidateAsync(auth));
    }

    [Fact]
    public async Task SilentValidate_TransientRefreshFailure_ReturnsTrueSoOfflineDataStaysAvailable()
    {
        var auth = Substitute.For<IAuthService>();
        auth.GetStoredTokensAsync().Returns(("access", "refresh"));
        auth.IsTokenExpiredAsync().Returns(true);
        auth.RefreshTokenAsync().Returns(TokenRefreshResult.Transient("offline"));
        var vm = new TestableBaseViewModel();

        Assert.True(await vm.ValidateSilentAsync(auth));
    }

    [Fact]
    public async Task SilentValidate_InvalidSession_ReturnsFalse()
    {
        var auth = Substitute.For<IAuthService>();
        auth.GetStoredTokensAsync().Returns(("access", "refresh"));
        auth.IsTokenExpiredAsync().Returns(true);
        auth.RefreshTokenAsync().Returns(TokenRefreshResult.InvalidSession("revoked"));
        var vm = new TestableBaseViewModel();

        Assert.False(await vm.ValidateSilentAsync(auth));
    }

    [Fact]
    public async Task SilentValidate_RefreshSuccess_ReturnsTrue()
    {
        var auth = Substitute.For<IAuthService>();
        auth.GetStoredTokensAsync().Returns(("access", "refresh"));
        auth.IsTokenExpiredAsync().Returns(true);
        auth.RefreshTokenAsync().Returns(TokenRefreshResult.Ok(new TokenResponseDTO("n", "r"), "ok"));
        var vm = new TestableBaseViewModel();

        Assert.True(await vm.ValidateSilentAsync(auth));
    }
}
