using MobileApp.Services;
using MobileApp.ViewModels;
using NSubstitute;
using Shared.DTOs;
using Xunit;

namespace MobileApp.Tests.ViewModels;

public sealed class LoginViewModelTests
{
    private static LoginViewModel Create(
        out IAuthService auth,
        out INavigationService navigation)
    {
        auth = Substitute.For<IAuthService>();
        navigation = Substitute.For<INavigationService>();
        auth.IsBiometricEnabledAsync().Returns(false);
        return new LoginViewModel(auth, navigation);
    }

    [Fact]
    public async Task Login_EmptyEmail_ShowsValidationError()
    {
        var vm = Create(out var auth, out var navigation);
        vm.Password = "secret";

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("email", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        await auth.DidNotReceive().LoginAsync(Arg.Any<string>(), Arg.Any<string>());
        await navigation.DidNotReceive().ShowMainTabsAsync();
    }

    [Fact]
    public async Task Login_EmptyPassword_ShowsValidationError()
    {
        var vm = Create(out var auth, out _);
        vm.Email = "user@test.com";

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("password", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        await auth.DidNotReceive().LoginAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Login_InvalidEmail_ShowsValidationError()
    {
        var vm = Create(out var auth, out _);
        vm.Email = "not-an-email";
        vm.Password = "secret";

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("valid email", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        await auth.DidNotReceive().LoginAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Login_Success_NavigatesToMainTabs()
    {
        var vm = Create(out var auth, out var navigation);
        vm.Email = "user@test.com";
        vm.Password = "secret";
        auth.LoginAsync("user@test.com", "secret")
            .Returns((true, new TokenResponseDTO("a", "r"), "ok"));

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.False(vm.IsBusy);
        await navigation.Received(1).ShowMainTabsAsync();
        await auth.DidNotReceive().EnableBiometricAuthenticationAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Login_SuccessWithBiometricEnabled_StoresCredentials()
    {
        var vm = Create(out var auth, out var navigation);
        vm.Email = "user@test.com";
        vm.Password = "secret";
        vm.BiometricEnabled = true;
        auth.LoginAsync("user@test.com", "secret")
            .Returns((true, new TokenResponseDTO("a", "r"), "ok"));

        await vm.LoginCommand.ExecuteAsync(null);

        await auth.Received().EnableBiometricAuthenticationAsync("user@test.com", "secret");
        await navigation.Received().ShowMainTabsAsync();
    }

    [Fact]
    public async Task Login_Failure_ShowsServerMessage()
    {
        var vm = Create(out var auth, out var navigation);
        vm.Email = "user@test.com";
        vm.Password = "secret";
        auth.LoginAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns((false, (TokenResponseDTO?)null, "Invalid email or password"));

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal("Invalid email or password", vm.ErrorMessage);
        await navigation.DidNotReceive().ShowMainTabsAsync();
    }

    [Fact]
    public void TogglePasswordVisibility_FlipsFlag()
    {
        var vm = Create(out _, out _);
        Assert.False(vm.IsPasswordVisible);

        vm.TogglePasswordVisibilityCommand.Execute(null);

        Assert.True(vm.IsPasswordVisible);
    }

    [Fact]
    public async Task ForgotPassword_SendsResetForValidEmail()
    {
        var vm = Create(out var auth, out var navigation);
        navigation.DisplayPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Keyboard?>())
            .Returns("user@test.com");
        auth.ForgotPasswordAsync("user@test.com").Returns((true, "sent"));

        await vm.ForgotPasswordCommand.ExecuteAsync(null);

        await auth.Received().ForgotPasswordAsync("user@test.com");
        await navigation.Received().DisplayAlertAsync("Reset Link Sent", "sent", "OK");
    }
}
