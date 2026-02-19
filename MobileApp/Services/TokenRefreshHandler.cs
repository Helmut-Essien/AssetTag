using System.Net;
using System.Net.Http.Headers;

namespace MobileApp.Services
{
    public class TokenRefreshHandler : DelegatingHandler
    {
        private readonly IAuthService _authService;

        public TokenRefreshHandler(IAuthService authService)
        {
            _authService = authService;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Get current access token
            var (accessToken, _) = await _authService.GetStoredTokensAsync();

            // If no token, proceed without authentication
            if (string.IsNullOrEmpty(accessToken))
            {
                return await base.SendAsync(request, cancellationToken);
            }

            // Check if token is expired or about to expire
            if (await _authService.IsTokenExpiredAsync())
            {
                // Try to refresh the token
                var (refreshSuccess, newTokens, _) = await _authService.RefreshTokenAsync();
                
                if (refreshSuccess && newTokens != null)
                {
                    accessToken = newTokens.AccessToken;
                }
                else
                {
                    // Refresh failed, clear tokens and return unauthorized
                    _authService.ClearTokens();
                    
                    // Navigate to login page on main thread to avoid crashes
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Shell.Current.GoToAsync("//LoginPage");
                    });
                    
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    {
                        Content = new StringContent("Session expired. Please login again.")
                    };
                }
            }

            // Attach bearer token to request
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Send the request
            var response = await base.SendAsync(request, cancellationToken);

            // If we get 401 Unauthorized, try to refresh token once
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var (refreshSuccess, newTokens, _) = await _authService.RefreshTokenAsync();
                
                if (refreshSuccess && newTokens != null)
                {
                    // Retry request with new token
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newTokens.AccessToken);
                    response = await base.SendAsync(request, cancellationToken);
                }
                else
                {
                    // Refresh failed, clear tokens and navigate to login
                    _authService.ClearTokens();
                    
                    // Navigate to login page on main thread to avoid crashes
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Shell.Current.GoToAsync("//LoginPage");
                    });
                }
            }

            return response;
        }
    }
}