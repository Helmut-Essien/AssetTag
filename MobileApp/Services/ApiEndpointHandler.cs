using MobileApp.Configuration;

namespace MobileApp.Services;

/// <summary>
/// Rewrites requests aimed at the configured primary host to the current
/// <see cref="ApiEndpointSelector"/> base URL (DEBUG fallback).
/// Absolute URIs to other hosts (e.g. a fallback ping) are left unchanged.
/// Health pings must use a client without this handler so a primary probe
/// is not rewritten to fallback.
/// </summary>
public sealed class ApiEndpointHandler : DelegatingHandler
{
    private readonly ApiEndpointSelector _selector;

    public ApiEndpointHandler(ApiEndpointSelector selector)
    {
        _selector = selector;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is { IsAbsoluteUri: true } uri)
        {
            var primary = ToBaseUri(_selector.PrimaryApiUrl);
            var current = ToBaseUri(_selector.BaseUrl);
            if (SameOrigin(uri, primary) && !SameOrigin(uri, current))
                request.RequestUri = new Uri(current, uri.PathAndQuery);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static Uri ToBaseUri(string url)
    {
        if (!url.EndsWith('/'))
            url += "/";
        return new Uri(url, UriKind.Absolute);
    }

    private static bool SameOrigin(Uri a, Uri b) =>
        string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase) &&
        a.Port == b.Port;
}
