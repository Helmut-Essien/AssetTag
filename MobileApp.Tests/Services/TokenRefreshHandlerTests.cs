using System.Net;
using System.Net.Http.Headers;
using MobileApp.Services;
using MobileApp.Tests.Helpers;
using NSubstitute;
using Shared.DTOs;
using Xunit;

namespace MobileApp.Tests.Services;

public sealed class TokenRefreshHandlerTests
{
    [Fact]
    public async Task SendAsync_AttachesBearerToken()
    {
        var auth = Substitute.For<IAuthService>();
        auth.GetStoredTokensAsync().Returns(("access-1", "refresh-1"));
        auth.IsTokenExpiredAsync().Returns(false);

        var inner = new TestHttpMessageHandler();
        inner.Respond("/data", HttpStatusCode.OK, "ok");
        var client = new HttpClient(new TokenRefreshHandler(auth) { InnerHandler = inner })
        {
            BaseAddress = new Uri("https://api.test/")
        };

        var response = await client.GetAsync("data");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("Bearer access-1", inner.Requests.Single().Authorization);
    }

    [Fact]
    public async Task SendAsync_NoToken_ForwardsWithoutAuthorization()
    {
        var auth = Substitute.For<IAuthService>();
        auth.GetStoredTokensAsync().Returns(((string?)null, (string?)null));

        var inner = new TestHttpMessageHandler();
        inner.Respond("/data", HttpStatusCode.OK, "ok");
        var client = new HttpClient(new TokenRefreshHandler(auth) { InnerHandler = inner })
        {
            BaseAddress = new Uri("https://api.test/")
        };

        await client.GetAsync("data");

        Assert.Null(inner.Requests.Single().Authorization);
        await auth.DidNotReceive().RefreshTokenAsync();
    }

    [Fact]
    public async Task SendAsync_ExpiredToken_RefreshesBeforeRequest()
    {
        var auth = Substitute.For<IAuthService>();
        auth.GetStoredTokensAsync().Returns(("old", "refresh"));
        auth.IsTokenExpiredAsync().Returns(true);
        auth.RefreshTokenAsync().Returns(TokenRefreshResult.Ok(new TokenResponseDTO("new", "refresh2"), "ok"));

        var inner = new TestHttpMessageHandler();
        inner.Respond("/data", HttpStatusCode.OK, "ok");
        var client = new HttpClient(new TokenRefreshHandler(auth) { InnerHandler = inner })
        {
            BaseAddress = new Uri("https://api.test/")
        };

        await client.GetAsync("data");

        Assert.Equal("Bearer new", inner.Requests.Single().Authorization);
    }

    [Fact]
    public async Task SendAsync_TransientRefreshFailure_SendsOriginalToken()
    {
        var auth = Substitute.For<IAuthService>();
        auth.GetStoredTokensAsync().Returns(("old", "refresh"));
        auth.IsTokenExpiredAsync().Returns(true);
        auth.RefreshTokenAsync().Returns(TokenRefreshResult.Transient("offline"));

        var inner = new TestHttpMessageHandler();
        inner.Respond("/data", HttpStatusCode.OK, "ok");
        var client = new HttpClient(new TokenRefreshHandler(auth) { InnerHandler = inner })
        {
            BaseAddress = new Uri("https://api.test/")
        };

        var response = await client.GetAsync("data");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("Bearer old", inner.Requests.Single().Authorization);
    }

    [Fact]
    public async Task SendAsync_InvalidSession_ReturnsUnauthorizedWithoutCallingApi()
    {
        var auth = Substitute.For<IAuthService>();
        auth.GetStoredTokensAsync().Returns(("old", "refresh"));
        auth.IsTokenExpiredAsync().Returns(true);
        auth.RefreshTokenAsync().Returns(TokenRefreshResult.InvalidSession("expired"));

        var inner = new TestHttpMessageHandler();
        inner.Respond("/data", HttpStatusCode.OK, "ok");
        var client = new HttpClient(new TokenRefreshHandler(auth) { InnerHandler = inner })
        {
            BaseAddress = new Uri("https://api.test/")
        };

        var response = await client.GetAsync("data");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(inner.Requests);
    }

    [Fact]
    public async Task SendAsync_401_RetriesWithClonedBody()
    {
        var auth = Substitute.For<IAuthService>();
        auth.GetStoredTokensAsync().Returns(("old", "refresh"));
        auth.IsTokenExpiredAsync().Returns(false);
        auth.RefreshTokenAsync().Returns(TokenRefreshResult.Ok(new TokenResponseDTO("new", "refresh2"), "ok"));

        var attempts = 0;
        var inner = new TestHttpMessageHandler
        {
            Fallback = request =>
            {
                attempts++;
                if (attempts == 1)
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("saved")
                };
            }
        };

        var client = new HttpClient(new TokenRefreshHandler(auth) { InnerHandler = inner })
        {
            BaseAddress = new Uri("https://api.test/")
        };

        using var content = new StringContent("""{"name":"asset"}""");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = await client.PostAsync("api/assets", content);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(2, attempts);
        Assert.Equal("Bearer new", inner.Requests[1].Authorization);
    }
}
