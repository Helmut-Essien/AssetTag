using Shared.DTOs;
using System.Net.Http.Json;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;

namespace MobileApp.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private const string PRIMARY_API_URL = "https://mugassetapi.runasp.net/";
        private const string FALLBACK_API_URL = "https://localhost:7135/"; // Development fallback
        private const string ACCESS_TOKEN_KEY = "access_token";
        private const string REFRESH_TOKEN_KEY = "refresh_token";
        private const string BIOMETRIC_ENABLED_KEY = "biometric_enabled";
        private string _currentBaseUrl = PRIMARY_API_URL;
        private static readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        public AuthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(_currentBaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
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
                if (await TryPingApi(PRIMARY_API_URL))
                {
                    // Only update if it's different from current
                    if (_currentBaseUrl != PRIMARY_API_URL)
                    {
                        _currentBaseUrl = PRIMARY_API_URL;
                        // Don't modify the existing HttpClient - it's already been used
                        // The BaseAddress is set in the constructor and shouldn't change
                    }
                    System.Diagnostics.Debug.WriteLine($"Connected to PRIMARY: {PRIMARY_API_URL}");
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"Primary API failed: {PRIMARY_API_URL}");
                
                // Only try fallback if on emulator/development
                #if DEBUG
                if (await TryPingApi(FALLBACK_API_URL))
                {
                    if (_currentBaseUrl != FALLBACK_API_URL)
                    {
                        _currentBaseUrl = FALLBACK_API_URL;
                        // Don't modify the existing HttpClient - it's already been used
                    }
                    System.Diagnostics.Debug.WriteLine($"Connected to FALLBACK: {FALLBACK_API_URL}");
                    return true;
                }
                System.Diagnostics.Debug.WriteLine($"Fallback API failed: {FALLBACK_API_URL}");
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
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)); // Increased timeout
                
                // Use a properly configured HttpClient
                using var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                    {
                        #if DEBUG
                        // Accept all certificates in DEBUG mode
                        return true;
                        #else
                        // In production, validate properly
                        return errors == System.Net.Security.SslPolicyErrors.None;
                        #endif
                    }
                };
                
                using var tempClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri(baseUrl),
                    Timeout = TimeSpan.FromSeconds(15)
                };
                
                System.Diagnostics.Debug.WriteLine($"Pinging: {baseUrl}api/test/ping");
                var response = await tempClient.GetAsync("api/test/ping", cts.Token);
                
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
                var (accessToken, refreshToken) = GetStoredTokens();

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                {
                    // No tokens to revoke, just clear local storage
                    ClearTokens();
                    return (true, "Logged out successfully");
                }

                // Check internet connectivity
                if (!await IsConnectedToInternet())
                {
                    // Offline - just clear local tokens
                    ClearTokens();
                    return (true, "Logged out locally (offline)");
                }

                // Try to revoke tokens on server
                try
                {
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    var tokenDto = new TokenResponseDTO(accessToken, refreshToken);
                    var response = await _httpClient.PostAsJsonAsync("api/auth/logout", tokenDto);

                    // Clear tokens regardless of server response
                    ClearTokens();

                    if (response.IsSuccessStatusCode)
                    {
                        return (true, "Logged out successfully");
                    }
                    else
                    {
                        // Server logout failed but local tokens cleared
                        return (true, "Logged out locally");
                    }
                }
                catch
                {
                    // Network error - clear local tokens anyway
                    ClearTokens();
                    return (true, "Logged out locally");
                }
            }
            catch (Exception ex)
            {
                // Always clear tokens on logout attempt
                ClearTokens();
                return (true, $"Logged out with warning: {ex.Message}");
            }
        }

        

        public void SaveTokens(string accessToken, string refreshToken)
        {
            SecureStorage.SetAsync(ACCESS_TOKEN_KEY, accessToken);
            SecureStorage.SetAsync(REFRESH_TOKEN_KEY, refreshToken);
        }

        public (string? AccessToken, string? RefreshToken) GetStoredTokens()
        {
            try
            {
                var accessToken = SecureStorage.GetAsync(ACCESS_TOKEN_KEY).Result;
                var refreshToken = SecureStorage.GetAsync(REFRESH_TOKEN_KEY).Result;
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
                var (accessToken, _) = GetStoredTokens();
                
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
                var (currentAccessToken, refreshToken) = GetStoredTokens();

                if (string.IsNullOrEmpty(refreshToken))
                {
                    return (false, null, "No refresh token available. Please login again.");
                }

                // Check if token was already refreshed by another call
                var (latestAccessToken, _) = GetStoredTokens();
                if (!string.IsNullOrEmpty(latestAccessToken) && latestAccessToken != currentAccessToken)
                {
                    // Token was already refreshed
                    return (true, new TokenResponseDTO(latestAccessToken, refreshToken), "Token already refreshed");
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
            catch (Exception)
            {
                // If biometric fails, fall back to allowing access
                // In production, you might want to handle this differently
                return true;
            }
        }

        public async Task EnableBiometricAuthenticationAsync()
        {
            await SecureStorage.SetAsync(BIOMETRIC_ENABLED_KEY, "true");
        }

        public async Task DisableBiometricAuthenticationAsync()
        {
            SecureStorage.Remove(BIOMETRIC_ENABLED_KEY);
        }

        public async Task<bool> IsBiometricEnabledAsync()
        {
            var enabled = await SecureStorage.GetAsync(BIOMETRIC_ENABLED_KEY);
            return enabled == "true";
        }
    }
}