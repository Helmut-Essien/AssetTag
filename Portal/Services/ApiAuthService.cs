using Shared.DTOs;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using static Portal.Services.IApiAuthService;

namespace Portal.Services;

public sealed class ApiAuthService : IApiAuthService
{
    private readonly IHttpClientFactory _http;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiAuthService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;


    public ApiAuthService(HttpClient httpClient, ILogger<ApiAuthService> logger, IHttpClientFactory http)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _http = http;
    }

    public async Task<TokenResponseDTO?> LoginAsync(LoginDTO dto, CancellationToken cancellationToken = default)
    {
        var client = _http.CreateClient("AssetTagApi");
        using var res = await client.PostAsJsonAsync("api/auth/login", dto, cancellationToken).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<TokenResponseDTO>(cancellationToken: cancellationToken).ConfigureAwait(false);
        //var token = await res.Content.ReadFromJsonAsync<TokenResponseDTO>(cancellationToken: cancellationToken);
        //return token;
    }

    public async Task<TokenResponseDTO?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var client = _http.CreateClient("AssetTagApi");
        var req = new TokenResponseDTO(string.Empty, refreshToken);
        using var res = await client.PostAsJsonAsync("api/auth/refresh-token", req, cancellationToken).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<TokenResponseDTO>(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RegisterAsync(RegisterDTO registerDto, CancellationToken cancellationToken = default)
    {
        var client = _http.CreateClient("AssetTagApi");
        using var res = await client.PostAsJsonAsync("api/auth/register", registerDto, cancellationToken).ConfigureAwait(false);
        return res.IsSuccessStatusCode;
    }

    public async Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var client = _http.CreateClient("AssetTagApi");
        var req = new TokenResponseDTO(string.Empty, refreshToken);
        using var res = await client.PostAsJsonAsync("api/auth/revoke", req, cancellationToken).ConfigureAwait(false);
        return res.IsSuccessStatusCode;
    }

    public async Task<ForgotPasswordResponse?> ForgotPasswordAsync(ForgotPasswordDTO dto, CancellationToken cancellationToken = default)
    {
        var client = _http.CreateClient("AssetTagApi");
        using var res = await client.PostAsJsonAsync("api/auth/forgot-password", dto, cancellationToken).ConfigureAwait(false);

        if (res.IsSuccessStatusCode)
        {
            return await res.Content.ReadFromJsonAsync<ForgotPasswordResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // Even if not successful, return a default response for security
        return new ForgotPasswordResponse { Message = "If the email exists, a password reset link has been sent." };
    }

    public async Task<ResetPasswordResponse?> ResetPasswordAsync(ResetPasswordDTO dto, CancellationToken cancellationToken = default)
    {
        var client = _http.CreateClient("AssetTagApi");
        using var res = await client.PostAsJsonAsync("api/auth/reset-password", dto, cancellationToken).ConfigureAwait(false);

        if (res.IsSuccessStatusCode)
        {
            return new ResetPasswordResponse { Success = true, Message = "Password has been reset successfully." };
        }
        else
        {
            // Try to read error message from response
            var errorResponse = await res.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return new ResetPasswordResponse
            {
                Success = false,
                Message = errorResponse?.Message ?? "Failed to reset password. The link may have expired."
            };
        }

    }




    public async Task<ApiResponse<string>> RegisterWithInvitationAsync(RegisterWithInvitationDTO dto)
    {
        try
        {
            _logger.LogInformation("Attempting to register user with invitation for email: {Email}", dto.Email);

            var client = _http.CreateClient("AssetTagApi");
            var response = await client.PostAsJsonAsync("api/auth/register-with-invitation", dto);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Registration response status: {StatusCode}, content: {Content}",
                response.StatusCode, content);

            if (response.IsSuccessStatusCode)
            {
                // Try to parse the success response
                try
                {
                    var successResponse = JsonSerializer.Deserialize<AuthSuccessResponse>(content, _jsonOptions);
                    return new ApiResponse<string>
                    {
                        Success = true,
                        Message = successResponse?.Message ?? "Registration successful."
                    };
                }
                catch (JsonException)
                {
                    // Fallback if response format is different
                    return new ApiResponse<string>
                    {
                        Success = true,
                        Message = "Registration successful!"
                    };
                }
            }
            else
            {
                // Try to parse error response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<AuthErrorResponse>(content, _jsonOptions);
                    if (errorResponse != null)
                    {
                        // Combine message and errors if available
                        var errorMessage = errorResponse.Message;
                        if (errorResponse.Errors != null && errorResponse.Errors.Any())
                        {
                            errorMessage += " " + string.Join(" ", errorResponse.Errors);
                        }

                        return new ApiResponse<string>
                        {
                            Success = false,
                            Message = errorMessage
                        };
                    }
                }
                catch (JsonException)
                {
                    // Fallback error message
                    _logger.LogWarning("Failed to parse error response: {Content}", content);
                }

                // Default error message based on status code
                return new ApiResponse<string>
                {
                    Success = false,
                    Message = response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.BadRequest => "Invalid registration data. Please check your information.",
                        System.Net.HttpStatusCode.Conflict => "A user with this email already exists.",
                        System.Net.HttpStatusCode.NotFound => "Invalid or expired invitation.",
                        _ => "Failed to register. Please try again."
                    }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering with invitation for {Email}", dto.Email);
            return new ApiResponse<string>
            {
                Success = false,
                Message = $"An error occurred during registration: {ex.Message}"
            };
        }
    }

    // Helper classes for response parsing
    private class AuthSuccessResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    private class AuthErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public IEnumerable<string> Errors { get; set; } = Enumerable.Empty<string>();
    }
}
   