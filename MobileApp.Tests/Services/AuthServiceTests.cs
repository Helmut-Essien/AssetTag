using System.Net;
using MobileApp.Configuration;
using MobileApp.Services;
using MobileApp.Tests.Helpers;
using Microsoft.Extensions.Options;
using Shared.DTOs;
using Xunit;

namespace MobileApp.Tests.Services;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task Login_NoNetwork_ReturnsOfflineMessage()
    {
        var handler = new TestHttpMessageHandler();
        var network = new FakeNetworkAccess { HasInternetAccess = false };
        var sut = ServiceTestFactory.AuthService(handler, network: network);

        var (success, token, message) = await sut.LoginAsync("user@test.com", "secret");

        Assert.False(success);
        Assert.Null(token);
        Assert.Contains("internet", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(handler.Requests, r => r.Uri?.AbsolutePath.Contains("login") == true);
    }

    [Fact]
    public async Task Login_Success_StoresTokensAndSessionEmail()
    {
        var handler = new TestHttpMessageHandler();
        var storage = new FakeSecureStorage();
        handler.RespondJson("api/auth/login", HttpStatusCode.OK, new TokenResponseDTO("access", "refresh"));
        var sut = ServiceTestFactory.AuthService(handler, storage);

        var (success, token, _) = await sut.LoginAsync("user@test.com", "secret");

        Assert.True(success);
        Assert.Equal("access", token!.AccessToken);
        Assert.Equal("access", await storage.GetAsync("access_token"));
        Assert.Equal("refresh", await storage.GetAsync("refresh_token"));
        Assert.Equal("user@test.com", await storage.GetAsync("session_email"));
    }

    [Fact]
    public async Task Login_Unauthorized_ReturnsInvalidCredentials()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondJson("api/auth/login", HttpStatusCode.Unauthorized, new ErrorResponse { Message = "Invalid email or password" });
        var sut = ServiceTestFactory.AuthService(handler);

        var (success, _, message) = await sut.LoginAsync("user@test.com", "wrong");

        Assert.False(success);
        Assert.Contains("Invalid", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshToken_401_ClearsTokensAndMarksInvalidSession()
    {
        var handler = new TestHttpMessageHandler();
        var storage = new FakeSecureStorage();
        await storage.SetAsync("access_token", JwtTestFactory.CreateExpiredToken());
        await storage.SetAsync("refresh_token", "refresh");
        handler.Respond("api/auth/refresh-token", HttpStatusCode.Unauthorized);
        var sut = ServiceTestFactory.AuthService(handler, storage);

        var result = await sut.RefreshTokenAsync();

        Assert.False(result.Succeeded);
        Assert.False(result.IsTransientFailure);
        Assert.False(storage.Contains("access_token"));
        Assert.False(storage.Contains("refresh_token"));
    }

    [Fact]
    public async Task RefreshToken_500_KeepsTokensAsTransientFailure()
    {
        var handler = new TestHttpMessageHandler();
        var storage = new FakeSecureStorage();
        await storage.SetAsync("access_token", JwtTestFactory.CreateExpiredToken());
        await storage.SetAsync("refresh_token", "refresh");
        handler.Respond("api/auth/refresh-token", HttpStatusCode.InternalServerError);
        var sut = ServiceTestFactory.AuthService(handler, storage);

        var result = await sut.RefreshTokenAsync();

        Assert.False(result.Succeeded);
        Assert.True(result.IsTransientFailure);
        Assert.True(storage.Contains("refresh_token"));
    }

    [Fact]
    public async Task RefreshToken_Offline_IsTransientAndKeepsSession()
    {
        var handler = new TestHttpMessageHandler();
        var storage = new FakeSecureStorage();
        var network = new FakeNetworkAccess { HasInternetAccess = false };
        await storage.SetAsync("access_token", JwtTestFactory.CreateExpiredToken());
        await storage.SetAsync("refresh_token", "refresh");
        var sut = ServiceTestFactory.AuthService(handler, storage, network);

        var result = await sut.RefreshTokenAsync();

        Assert.True(result.IsTransientFailure);
        Assert.True(storage.Contains("refresh_token"));
    }

    [Fact]
    public async Task RefreshToken_Success_StoresNewTokens()
    {
        var handler = new TestHttpMessageHandler();
        var storage = new FakeSecureStorage();
        await storage.SetAsync("access_token", JwtTestFactory.CreateExpiredToken());
        await storage.SetAsync("refresh_token", "old-refresh");
        handler.RespondJson("api/auth/refresh-token", HttpStatusCode.OK, new TokenResponseDTO("new-access", "new-refresh"));
        var sut = ServiceTestFactory.AuthService(handler, storage);

        var result = await sut.RefreshTokenAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("new-access", await storage.GetAsync("access_token"));
        Assert.Equal("new-refresh", await storage.GetAsync("refresh_token"));
    }

    [Fact]
    public async Task RefreshToken_RecentlyIssuedAccessToken_ReusesWithoutHttp()
    {
        var handler = new TestHttpMessageHandler();
        var storage = new FakeSecureStorage();
        await storage.SetAsync("access_token", JwtTestFactory.CreateFreshToken());
        await storage.SetAsync("refresh_token", "refresh");
        var sut = ServiceTestFactory.AuthService(handler, storage);

        var result = await sut.RefreshTokenAsync();

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(handler.Requests, r => r.Uri?.AbsolutePath.Contains("refresh-token") == true);
    }

    [Fact]
    public async Task IsTokenExpired_TreatsFiveMinuteBufferAsExpired()
    {
        var handler = new TestHttpMessageHandler();
        var storage = new FakeSecureStorage();
        await storage.SetAsync("access_token", JwtTestFactory.CreateToken(expires: DateTime.UtcNow.AddMinutes(2)));
        await storage.SetAsync("refresh_token", "refresh");
        var sut = ServiceTestFactory.AuthService(handler, storage);

        Assert.True(await sut.IsTokenExpiredAsync());
    }

    [Fact]
    public async Task Logout_ClearsTokensAndBiometricKeys()
    {
        var handler = new TestHttpMessageHandler();
        var storage = new FakeSecureStorage();
        await storage.SetAsync("access_token", "a");
        await storage.SetAsync("refresh_token", "r");
        await storage.SetAsync("session_email", "user@test.com");
        await storage.SetAsync("biometric_enabled", "true");
        var sut = ServiceTestFactory.AuthService(handler, storage);

        var (success, _) = await sut.LogoutAsync();

        Assert.True(success);
        Assert.False(storage.Contains("access_token"));
        Assert.False(storage.Contains("biometric_enabled"));
        Assert.False(storage.Contains("session_email"));
    }

    [Fact]
    public async Task IsConnectedToInternet_PrimaryPingSuccess_SetsPrimaryBaseUrl()
    {
        var handler = new TestHttpMessageHandler();
        var settings = new ApiSettings
        {
            PrimaryApiUrl = "https://primary.test/",
            FallbackApiUrl = "https://fallback.test/",
            RequestTimeout = 30
        };
        var storage = new FakeSecureStorage();
        var network = new FakeNetworkAccess();
        var selector = new ApiEndpointSelector(Options.Create(settings));
        handler.Respond("api/test/ping", HttpStatusCode.OK, "pong");
        var sut = new AuthService(
            ServiceTestFactory.HttpFactory(handler, settings.PrimaryApiUrl),
            Options.Create(settings),
            selector,
            storage,
            network);

        Assert.True(await sut.IsConnectedToInternet());
        Assert.Equal("https://primary.test/", selector.BaseUrl);
    }
}
