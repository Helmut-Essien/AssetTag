using Microsoft.Extensions.Options;
using MobileApp.Configuration;
using MobileApp.Services;
using NSubstitute;

namespace MobileApp.Tests.Helpers;

public static class ServiceTestFactory
{
    public static IHttpClientFactory HttpFactory(TestHttpMessageHandler handler, string baseUrl = "https://api.test/")
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        });
        return factory;
    }

    public static AuthService AuthService(
        TestHttpMessageHandler handler,
        FakeSecureStorage? storage = null,
        FakeNetworkAccess? network = null,
        ApiSettings? settings = null)
    {
        storage ??= new FakeSecureStorage();
        network ??= new FakeNetworkAccess();
        settings ??= new ApiSettings
        {
            PrimaryApiUrl = "https://primary.test/",
            FallbackApiUrl = "https://fallback.test/",
            RequestTimeout = 30
        };

        handler.Respond("api/test/ping", System.Net.HttpStatusCode.OK, "pong");

        var selector = new ApiEndpointSelector(Options.Create(settings));
        return new AuthService(
            HttpFactory(handler, settings.PrimaryApiUrl),
            Options.Create(settings),
            selector,
            storage,
            network);
    }
}
