using System.Net;
using System.Text;
using System.Text.Json;

namespace MobileApp.Tests.Helpers;

public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    public List<CapturedRequest> Requests { get; } = new();

    public Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> Routes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Func<HttpRequestMessage, HttpResponseMessage>? Fallback { get; set; }

    public void RespondJson(string pathContains, HttpStatusCode status, object body)
    {
        Routes[pathContains] = _ => Json(status, body);
    }

    public void Respond(string pathContains, HttpStatusCode status, string? content = null)
    {
        Routes[pathContains] = _ => new HttpResponseMessage(status)
        {
            Content = new StringContent(content ?? string.Empty, Encoding.UTF8, "text/plain")
        };
    }

    public static HttpResponseMessage Json(HttpStatusCode status, object body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(new CapturedRequest(request.Method, request.RequestUri, request.Headers.Authorization?.ToString()));

        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        foreach (var (key, responder) in Routes)
        {
            if (path.Contains(key, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(responder(request));
        }

        if (Fallback != null)
            return Task.FromResult(Fallback(request));

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    public sealed record CapturedRequest(HttpMethod Method, Uri? Uri, string? Authorization);
}
