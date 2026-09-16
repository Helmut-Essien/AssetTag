namespace AssetTag.Middleware;

/// <summary>
/// Development-only request diagnostics. Never register in Production — it logs
/// headers/cookies and buffers response bodies.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        _logger.LogInformation(
            "Request {Method} {Path}{Query}",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString);

        foreach (var header in context.Request.Headers)
        {
            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("X-Auth-Token", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Header {Key} = [redacted]", header.Key);
                continue;
            }

            _logger.LogDebug("Header {Key} = {Value}", header.Key, header.Value.ToString());
        }

        await _next(context);

        _logger.LogInformation(
            "Response {StatusCode} for {Method} {Path}",
            context.Response.StatusCode,
            context.Request.Method,
            context.Request.Path);
    }
}
