using MobileApp.Configuration;
using MobileApp.Services;
using MobileApp.Tests.Helpers;
using Microsoft.Extensions.Options;
using Xunit;

namespace MobileApp.Tests.Services;

public sealed class ApiEndpointHandlerTests
{
    [Fact]
    public async Task RewritesPrimaryHostToCurrentFallback()
    {
        var settings = new ApiSettings
        {
            PrimaryApiUrl = "https://primary.test/",
            FallbackApiUrl = "https://fallback.test/"
        };
        var selector = new ApiEndpointSelector(Options.Create(settings));
        selector.SetBaseUrl("https://fallback.test/");

        var inner = new TestHttpMessageHandler();
        inner.Respond("/api/assets", System.Net.HttpStatusCode.OK, "ok");
        var client = new HttpClient(new ApiEndpointHandler(selector) { InnerHandler = inner });

        await client.GetAsync("https://primary.test/api/assets?page=1");

        Assert.Equal("https://fallback.test/api/assets?page=1", inner.Requests.Single().Uri!.ToString());
    }

    [Fact]
    public async Task LeavesNonPrimaryHostsUnchanged()
    {
        var settings = new ApiSettings
        {
            PrimaryApiUrl = "https://primary.test/",
            FallbackApiUrl = "https://fallback.test/"
        };
        var selector = new ApiEndpointSelector(Options.Create(settings));
        selector.SetBaseUrl("https://fallback.test/");

        var inner = new TestHttpMessageHandler();
        inner.Respond("/api/test/ping", System.Net.HttpStatusCode.OK, "pong");
        var client = new HttpClient(new ApiEndpointHandler(selector) { InnerHandler = inner });

        await client.GetAsync("https://fallback.test/api/test/ping");

        Assert.Equal("https://fallback.test/api/test/ping", inner.Requests.Single().Uri!.ToString());
    }
}

public sealed class ApiEndpointSelectorTests
{
    [Fact]
    public void StartsOnPrimaryUrl()
    {
        var selector = new ApiEndpointSelector(Options.Create(new ApiSettings
        {
            PrimaryApiUrl = "https://primary.test/",
            FallbackApiUrl = "https://fallback.test/"
        }));

        Assert.Equal("https://primary.test/", selector.BaseUrl);
        Assert.Equal("https://primary.test/", selector.PrimaryApiUrl);
    }

    [Fact]
    public void SetBaseUrl_IsVisibleToReaders()
    {
        var selector = new ApiEndpointSelector(Options.Create(new ApiSettings
        {
            PrimaryApiUrl = "https://primary.test/"
        }));

        selector.SetBaseUrl("https://fallback.test/");

        Assert.Equal("https://fallback.test/", selector.BaseUrl);
    }
}
