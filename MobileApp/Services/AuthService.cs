using Shared.DTOs;
using System.Net.Http.Json;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using MobileApp.Configuration;

namespace MobileApp.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ApiSettings _apiSettings;
        private const string ACCESS_TOKEN_KEY = "access_token";
        private const string REFRESH_TOKEN_KEY = "refresh_token";
        private const string BIOMETRIC_ENABLED_KEY = "biometric_enabled";
        private string _currentBaseUrl;
        private static readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        public AuthService(HttpClient httpClient, IOptions<ApiSettings> apiSettings)
        {
            _httpClient = httpClient;
            _apiSettings = apiSettings.Value;
            _currentBaseUrl = _apiSettings.PrimaryApiUrl;
            _httpClient.BaseAddress = new Uri(_currentBaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_apiSettings.RequestTimeout);
        }

        public async Task<bool> IsConnectedToInternet()
        {
            try
            {
                var current = Connectivity.NetworkAccess;
                
                if (current != NetworkAccess.Internet)
                {
                    System.Diagnostics.Debug.WriteLine("No network access detected");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("Network access detected, testing API...");

                // Try primary API first
                if (await TryPingApi(_apiSettings.PrimaryApiUrl))
                {
                    // Only update if it's different from current
                    if (_currentBaseUrl != _apiSettings.PrimaryApiUrl)
                    {
                        _currentBaseUrl = _apiSettings.PrimaryApiUrl;
                        // Don't modify the existing HttpClient - it's already been used
                        // The BaseAddress is set in the constructor and shouldn't change
                    }
                    System.Diagnostics.Debug.WriteLine($"Connected to PRIMARY: {_apiSettings.PrimaryApiUrl}");
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"Primary API failed: {_apiSettings.PrimaryApiUrl}");
                
                // Only try fallback if on emulator/development
                #if DEBUG
                if (await TryPingApi(_apiSettings.FallbackApiUrl))
                {
                    if (_currentBaseUrl != _apiSettings.FallbackApiUrl)
                    {
                        _currentBaseUrl = _apiSettings.FallbackApiUrl;
                        // Don't modify the existing HttpClient - it's already been used
                    }
                    System.Diagnostics.Debug.WriteLine($"Connected to FALLBACK: {_apiSettings.FallbackApiUrl}");
                    return true;
                }
                System.Diagnostics.Debug.WriteLine($"Fallback API failed: {_apiSettings.FallbackApiUrl}");
                #endif

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IsConnectedToInternet Exception: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TryPingApi(string baseUrl)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                
                // Create a temporary request with the base URL to test
                var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl), "api/test/ping"));
                
                System.Diagnostics.Debug.WriteLine($"Pinging: {baseUrl}api/test/ping");
                
                // Reuse the main HttpClient but with a custom request
                var response = await _httpClient.SendAsync(request, cts.Token);
                
                System.Diagnostics.Debug.WriteLine($"Response: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (TaskCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Timeout pinging {baseUrl}: {ex.Message}");
                return false;
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTTP error pinging {baseUrl}: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pinging {baseUrl}: {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        public async Task<(bool Success, TokenResponseDTO? Token, string Message)> LoginAsync(string email, string password)
        {
            try
            {
                // Check internet connectivity first
                if (!await IsConnectedToInternet())
                {
                    return (false, null, "No internet connection. Please check your network and try again.");
                }

                var loginDto = new LoginDTO(email, password);

                var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginDto);

                if (response.IsSuccessStatusCode)
                {
                    var token = await response.Content.ReadFromJsonAsync<TokenResponseDTO>();

                    if (token != null)
                    {
                        SaveTokens(token.AccessToken, token.RefreshToken);
                        return (true, token, "Login successful");
                    }

                    return (false, null, "Invalid response from server");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Try to read error message from response
                    try
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(errorContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        return (false, null, errorResponse?.Message ?? "Invalid email or password");
                    }
                    catch
                    {
                        return (false, null, "Invalid email or password");
                    }
                }
                else
                {
                    return (false, null, $"Login failed: {response.StatusCode}");
                }
            }
            catch (TaskCanceledException)
            {
                return (false, null, "Request timeout. Please check your connection and try again.");
            }
            catch (HttpRequestException ex)
            {
                return (false, null, $"Network error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, null, $"An error occurred: {ex.Message}");
            }
        }
        public async Task<(bool Success, string Message)> LogoutAsync()
        {
            try
            {
                var (accessToken, refreshToken) = await GetStoredTokensAsync();

                // Always clear local tokens first
                ClearTokens();

                // If no tokens or offline, we're done
                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                {
                    return (true, "Logged out successfully");
                }

                if (!await IsConnectedToInternet())
                {
                    return (true, "Logged out successfully");
                }

                // Try to revoke tokens on server (best effort)
                try
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    var tokenDto = new TokenResponseDTO(accessToken, refreshToken);
                    await _httpClient.PostAsJsonAsync("api/auth/logout", tokenDto);
                }
                catch
                {
                    // Ignore server errors - local logout already succeeded
                }

                return (true, "Logged out successfully");
            }
            catch (Exception)
            {
                // Always clear tokens on logout attempt
                ClearTokens();
                return (true, "Logged out successfully");
            }
        }

        

        public void SaveTokens(string accessToken, string refreshToken)
        {
            SecureStorage.SetAsync(ACCESS_TOKEN_KEY, accessToken);
            SecureStorage.SetAsync(REFRESH_TOKEN_KEY, refreshToken);
        }

        public async Task<(string? AccessToken, string? RefreshToken)> GetStoredTokensAsync()
        {
            try
            {
                var accessToken = await SecureStorage.GetAsync(ACCESS_TOKEN_KEY);
                var refreshToken = await SecureStorage.GetAsync(REFRESH_TOKEN_KEY);
                return (accessToken, refreshToken);
            }
            catch
            {
                return (null, null);
            }
        }

        public void ClearTokens()
        {
            SecureStorage.Remove(ACCESS_TOKEN_KEY);
            SecureStorage.Remove(REFRESH_TOKEN_KEY);
        }

        public async Task<(bool Success, string Message)> ForgotPasswordAsync(string email)
        {
            try
            {
                // Check internet connectivity first
                if (!await IsConnectedToInternet())
                {
                    return (false, "No internet connection. Please check your network and try again.");
                }

                var forgotPasswordDto = new ForgotPasswordDTO { Email = email };
                
                var response = await _httpClient.PostAsJsonAsync("api/auth/forgot-password", forgotPasswordDto);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "If the email exists, a password reset link has been sent to your email address. Please check your email and use the link to reset your password in the portal.");
                }
                else
                {
                    // For security, always show success message even if email doesn't exist
                    return (true, "If the email exists, a password reset link has been sent to your email address. Please check your email and use the link to reset your password in the portal.");
                }
            }
            catch (TaskCanceledException)
            {
                return (false, "Request timeout. Please check your connection and try again.");
            }
            catch (HttpRequestException ex)
            {
                return (false, $"Network error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"An error occurred: {ex.Message}");
            }
        }

        public async Task<bool> IsTokenExpiredAsync()
        {
            try
            {
                var (accessToken, _) = await GetStoredTokensAsync();
                
                if (string.IsNullOrEmpty(accessToken))
                    return true;

                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(accessToken))
                    return true;

                var jwt = handler.ReadJwtToken(accessToken);
                
                // Check if token expires within next 5 minutes (buffer for network delays)
                return jwt.ValidTo <= DateTime.UtcNow.AddMinutes(5);
            }
            catch
            {
                return true; // If we can't read the token, consider it expired
            }
        }

        public async Task<(bool Success, TokenResponseDTO? Token, string Message)> RefreshTokenAsync()
        {
            // Prevent concurrent refresh attempts
            await _refreshLock.WaitAsync();
            
            try
            {
                // Get tokens INSIDE the lock to ensure we have the latest
                var (currentAccessToken, refreshToken) = await GetStoredTokensAsync();

                if (string.IsNullOrEmpty(refreshToken))
                {
                    return (false, null, "No refresh token available. Please login again.");
                }

                // Check if access token was recently refreshed (within last 10 seconds)
                if (!string.IsNullOrEmpty(currentAccessToken))
                {
                    var handler = new JwtSecurityTokenHandler();
                    if (handler.CanReadToken(currentAccessToken))
                    {
                        var jwt = handler.ReadJwtToken(currentAccessToken);
                        var tokenAge = DateTime.UtcNow - jwt.ValidFrom;
                        
                        if (tokenAge.TotalSeconds < 10)
                        {
                            // Token was just refreshed, reuse it
                            return (true, new TokenResponseDTO(currentAccessToken, refreshToken), "Using recently refreshed token");
                        }
                    }
                }

                // Check internet connectivity
                if (!await IsConnectedToInternet())
                {
                    return (false, null, "No internet connection. Please check your network.");
                }

                var tokenRequest = new TokenResponseDTO(string.Empty, refreshToken);
                
                var response = await _httpClient.PostAsJsonAsync("api/auth/refresh-token", tokenRequest);

                if (response.IsSuccessStatusCode)
                {
                    var newTokens = await response.Content.ReadFromJsonAsync<TokenResponseDTO>();
                    
                    if (newTokens != null)
                    {
                        SaveTokens(newTokens.AccessToken, newTokens.RefreshToken);
                        return (true, newTokens, "Token refreshed successfully");
                    }
                    
                    return (false, null, "Invalid response from server");
                }
                else
                {
                    // Refresh token is invalid or expired
                    ClearTokens();
                    return (false, null, "Session expired. Please login again.");
                }
            }
            catch (Exception ex)
            {
                return (false, null, $"Token refresh failed: {ex.Message}");
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        public async Task<bool> AuthenticateWithBiometricsAsync(string reason)
        {
            try
            {
                // Check if biometric authentication is available
                var isBiometricAvailable = await SecureStorage.GetAsync(BIOMETRIC_ENABLED_KEY);
                
                if (isBiometricAvailable != "true")
                {
                    return true; // Biometrics not enabled, allow access
                }

                // Request biometric authentication
                var authRequest = new AuthenticationRequest
                {
                    Title = "Authentication Required",
                    Reason = reason,
                    AllowAlternativeAuthentication = true
                };

                var result = await BiometricAuthentication.AuthenticateAsync(authRequest);
                return result.Authenticated;
            }
            catch (Exception ex)
            {
                // SECURITY FIX: Do NOT allow access on exception
                // Log the error for debugging
                System.Diagnostics.Debug.WriteLine($"Biometric authentication error: {ex.Message}");
                return false; // Deny access if biometric authentication fails
            }
        }

        public async Task EnableBiometricAuthenticationAsync()
        {
            await SecureStorage.SetAsync(BIOMETRIC_ENABLED_KEY, "true");
        }

        public Task DisableBiometricAuthenticationAsync()
        {
            SecureStorage.Remove(BIOMETRIC_ENABLED_KEY);
            return Task.CompletedTask;
        }

        public async Task<bool> IsBiometricEnabledAsync()
        {
            var enabled = await SecureStorage.GetAsync(BIOMETRIC_ENABLED_KEY);
            return enabled == "true";
        }
    }
}