
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Portal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared.DTOs;

namespace Portal.Services;

public sealed class TokenRefreshHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TokenRefreshHandler> _logger;
    private const string CookieScheme = "PortalCookie";

    // Per-user lock to prevent concurrent refresh attempts
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _userLocks = new();

    public TokenRefreshHandler(
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        ILogger<TokenRefreshHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var ctx = _httpContextAccessor.HttpContext;

        // If no authenticated user, proceed without token
        if (ctx?.User?.Identity?.IsAuthenticated != true)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // Get current access token
        var accessToken = ctx.User.FindFirst("AccessToken")?.Value;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("No access token found for authenticated user. Signing out.");
            await SignOutAsync(ctx);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        // Attach bearer token
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Make the request
        var response = await base.SendAsync(request, cancellationToken);

        // If not unauthorized, return response
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        _logger.LogInformation("Received 401 Unauthorized. Attempting token refresh for {Url}",
            request.RequestUri);

        // Attempt to refresh token
        var refreshed = await TryRefreshTokenAsync(ctx, accessToken, cancellationToken);

        if (!refreshed)
        {
            _logger.LogWarning("Token refresh failed. Signing out user.");
            await SignOutAsync(ctx);
            return response; // Return original 401 response
        }

        // Retry request with new token
        var newAccessToken = ctx.User.FindFirst("AccessToken")?.Value;
        if (!string.IsNullOrWhiteSpace(newAccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);
            _logger.LogInformation("Retrying request with refreshed token");
            return await base.SendAsync(request, cancellationToken);
        }

        return response;
    }

    private async Task<bool> TryRefreshTokenAsync(
        HttpContext ctx,
        string currentAccessToken,
        CancellationToken cancellationToken)
    {
        // Get user identifier for locking
        var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? ctx.User.FindFirst(ClaimTypes.Email)?.Value
                     ?? ctx.User.Identity?.Name
                     ?? "unknown";

        var userLock = _userLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

        // Try to acquire lock with timeout
        var lockAcquired = await userLock.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        if (!lockAcquired)
        {
            _logger.LogWarning("Could not acquire refresh lock for user {UserId} within timeout", userId);
            return false;
        }

        try
        {
            // Check if another request already refreshed the token
            var latestAccessToken = ctx.User.FindFirst("AccessToken")?.Value;
            if (!string.IsNullOrWhiteSpace(latestAccessToken) && latestAccessToken != currentAccessToken)
            {
                _logger.LogDebug("Token already refreshed by another request for user {UserId}", userId);
                return true; // Token was already refreshed
            }

            // Get refresh token
            var refreshToken = ctx.User.FindFirst("RefreshToken")?.Value;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                _logger.LogWarning("No refresh token found for user {UserId}", userId);
                return false;
            }

            // Call refresh endpoint using separate HttpClient (no handlers)
            using var authClient = _httpClientFactory.CreateClient("AuthApi");
            var refreshRequest = new TokenResponseDTO(string.Empty, refreshToken);

            var refreshResponse = await authClient.PostAsJsonAsync(
                "api/auth/refresh-token",
                refreshRequest,
                cancellationToken);

            if (!refreshResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Refresh token request failed with status {Status} for user {UserId}",
                    refreshResponse.StatusCode, userId);
                return false;
            }

            var tokenResponse = await refreshResponse.Content.ReadFromJsonAsync<TokenResponseDTO>(
                cancellationToken: cancellationToken);

            if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                _logger.LogWarning("Refresh token response was null or invalid for user {UserId}", userId);
                return false;
            }

            // Update cookie with new tokens
            await UpdateAuthenticationCookieAsync(ctx, tokenResponse);

            _logger.LogInformation("Successfully refreshed tokens for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh for user {UserId}", userId);
            return false;
        }
        finally
        {
            userLock.Release();
        }
    }

    private async Task UpdateAuthenticationCookieAsync(HttpContext ctx, TokenResponseDTO tokens)
    {
        // Get current authentication ticket
        var authenticateResult = await ctx.AuthenticateAsync(CookieScheme);

        if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
        {
            _logger.LogWarning("Failed to retrieve current authentication ticket");
            return;
        }

        // Create new claims with updated tokens
        var identity = authenticateResult.Principal.Identity as ClaimsIdentity;
        if (identity == null)
        {
            _logger.LogWarning("Identity is not a ClaimsIdentity");
            return;
        }

        // Remove old token claims
        var oldAccessClaim = identity.FindFirst("AccessToken");
        var oldRefreshClaim = identity.FindFirst("RefreshToken");

        if (oldAccessClaim != null) identity.RemoveClaim(oldAccessClaim);
        if (oldRefreshClaim != null) identity.RemoveClaim(oldRefreshClaim);

        // Add new token claims
        identity.AddClaim(new Claim("AccessToken", tokens.AccessToken));
        identity.AddClaim(new Claim("RefreshToken", tokens.RefreshToken));

        // Preserve authentication properties
        var properties = authenticateResult.Properties ?? new AuthenticationProperties();

        // Re-sign in with updated claims
        await ctx.SignInAsync(
            CookieScheme,
            new ClaimsPrincipal(identity),
            properties);

        _logger.LogDebug("Authentication cookie updated with new tokens");
    }

    private async Task SignOutAsync(HttpContext ctx)
    {
        try
        {
            await ctx.SignOutAsync(CookieScheme);
            _logger.LogInformation("User signed out due to authentication failure");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing out user");
        }
    }
}