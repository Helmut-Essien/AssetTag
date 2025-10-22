using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using AssetTag.DTOs; // if your Shared DTOs live under Shared.DTOs adjust namespace
using Portal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared.DTOs;

namespace Portal.Services;

public sealed class TokenRefreshHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IApiAuthService _authService;
    private readonly ILogger<TokenRefreshHandler> _logger;
    private const string CookieScheme = "PortalCookie";
    // per-user semaphore to avoid refresh storms
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public TokenRefreshHandler(
        IHttpContextAccessor httpContextAccessor,
        IApiAuthService authService,
        ILogger<TokenRefreshHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx?.User?.Identity?.IsAuthenticated == true)
        {
            var accessBefore = ctx.User.FindFirst("AccessToken")?.Value;
            if (!string.IsNullOrWhiteSpace(accessBefore))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessBefore);
            }

            var response = await base.SendAsync(CloneRequest(request), cancellationToken).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            // Need to attempt refresh
            var userKey = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? ctx.User.Identity?.Name
                          ?? Guid.NewGuid().ToString();

            var sem = _locks.GetOrAdd(userKey, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Another request may have refreshed tokens already — check current token
                var accessNow = ctx.User.FindFirst("AccessToken")?.Value;
                if (!string.IsNullOrWhiteSpace(accessNow) && accessNow != accessBefore)
                {
                    // retry with fresh token
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessNow);
                    return await base.SendAsync(CloneRequest(request), cancellationToken).ConfigureAwait(false);
                }

                var refreshToken = ctx.User.FindFirst("RefreshToken")?.Value;
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    _logger.LogInformation("No refresh token available for user {User}. Signing out.", userKey);
                    await ctx.SignOutAsync(CookieScheme).ConfigureAwait(false);
                    return response;
                }

                // Call portal service to refresh tokens using the API
                TokenResponseDTO? tokenResponse = null;
                try
                {
                    tokenResponse = await _authService.RefreshAsync(refreshToken, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error calling refresh endpoint for user {User}.", userKey);
                }

                if (tokenResponse == null)
                {
                    _logger.LogInformation("Refresh failed for user {User}. Signing out.", userKey);
                    await ctx.SignOutAsync(CookieScheme).ConfigureAwait(false);
                    return response;
                }

                // Re-issue auth cookie with new tokens while preserving other claims and auth properties
                var ticket = await ctx.AuthenticateAsync(CookieScheme).ConfigureAwait(false);
                var properties = ticket?.Properties ?? new AuthenticationProperties { IsPersistent = true };
                var principal = ticket?.Principal ?? ctx.User;

                // Build new ClaimsIdentity copying existing claims except token claims
                var newClaims = principal?.Claims
                    .Where(c => c.Type != "AccessToken" && c.Type != "RefreshToken")
                    .Select(c => new Claim(c.Type, c.Value, c.ValueType, c.Issuer))?.ToList()
                    ?? new System.Collections.Generic.List<Claim>();

                newClaims.Add(new Claim("AccessToken", tokenResponse.AccessToken ?? string.Empty));
                newClaims.Add(new Claim("RefreshToken", tokenResponse.RefreshToken ?? string.Empty));

                var newIdentity = new ClaimsIdentity(newClaims, CookieScheme);
                var newPrincipal = new ClaimsPrincipal(newIdentity);

                // Optionally adjust cookie expiry using properties
                if (!properties.ExpiresUtc.HasValue)
                {
                    properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8);
                }

                await ctx.SignInAsync(CookieScheme, newPrincipal, properties).ConfigureAwait(false);
                _logger.LogDebug("Refresh succeeded and cookie re-issued for user {User}.", userKey);

                // Retry original request with new access token
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
                return await base.SendAsync(CloneRequest(request), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                sem.Release();
            }
        }

        // No authenticated user - proceed normally
        return await base.SendAsync(CloneRequest(request), cancellationToken).ConfigureAwait(false);
    }

    // Clone HttpRequestMessage because a request can be sent only once
    private static HttpRequestMessage CloneRequest(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version
        };

        // copy content (if any)
        if (original.Content != null)
        {
            var ms = new MemoryStream();
            original.Content.CopyToAsync(ms).GetAwaiter().GetResult();
            ms.Position = 0;
            clone.Content = new StreamContent(ms);
            foreach (var header in original.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // copy headers
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        // copy properties if needed (not available in all runtimes) - skip to avoid compatibility issues
        return clone;
    }
}