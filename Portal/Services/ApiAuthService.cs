using Shared.DTOs;
using System.Net.Http.Json;
using System.Text.Json;

namespace Portal.Services;

public sealed class ApiAuthService : IApiAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiAuthService> _logger;

    // Static JSON options for performance
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiAuthService(IHttpClientFactory httpClientFactory, ILogger<ApiAuthService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AuthApi");
        _logger = logger;
    }

    public async Task<TokenResponseDTO?> LoginAsync(LoginDTO dto, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/login", dto, _jsonOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (errorContent.Contains("ACCOUNT_DEACTIVATED", StringComparison.OrdinalIgnoreCase) ||
                    errorContent.Contains("\"IsDeactivated\":true", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ApiException("Account deactivated", 401, "ACCOUNT_DEACTIVATED");
                }
            }
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TokenResponseDTO>(_jsonOptions, cancellationToken);
    }

    public async Task<TokenResponseDTO?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var request = new TokenResponseDTO(string.Empty, refreshToken);
        using var response = await _httpClient.PostAsJsonAsync("api/auth/refresh-token", request, _jsonOptions, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TokenResponseDTO>(_jsonOptions, cancellationToken)
            : null;
    }

    public async Task<bool> RegisterAsync(RegisterDTO registerDto, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/register", registerDto, _jsonOptions, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var request = new TokenResponseDTO(string.Empty, refreshToken);
        using var response = await _httpClient.PostAsJsonAsync("api/auth/revoke", request, _jsonOptions, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<ForgotPasswordResponse?> ForgotPasswordAsync(ForgotPasswordDTO dto, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/forgot-password", dto, _jsonOptions, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ForgotPasswordResponse>(_jsonOptions, cancellationToken)
            : new ForgotPasswordResponse { Message = "If the email exists, a password reset link has been sent." };
    }

    public async Task<ResetPasswordResponse?> ResetPasswordAsync(ResetPasswordDTO dto, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/reset-password", dto, _jsonOptions, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new ResetPasswordResponse { Success = true, Message = "Password reset successfully." };
        }

        var errorMessage = await GetErrorMessageFast(response, cancellationToken);
        return new ResetPasswordResponse
        {
            Success = false,
            Message = errorMessage ?? "Failed to reset password. The link may have expired."
        };
    }

    public async Task<ApiResponse<string>> RegisterWithInvitationAsync(RegisterWithInvitationDTO dto)
    {
        try
        {
            _logger.LogInformation("Registering user with invitation: {Email}", dto.Email);

            using var response = await _httpClient.PostAsJsonAsync("api/auth/register-with-invitation", dto, _jsonOptions);

            if (response.IsSuccessStatusCode)
            {
                return new ApiResponse<string>
                {
                    Success = true,
                    Message = "Registration successful!"
                };
            }

            var error = await GetRegistrationErrorFast(response);
            return new ApiResponse<string>
            {
                Success = false,
                Message = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration error for {Email}", dto.Email);
            return new ApiResponse<string>
            {
                Success = false,
                Message = "Registration failed. Please try again."
            };
        }
    }

    #region Performance Optimized Helper Methods

   

    private static async Task<string?> GetErrorMessageFast(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(_jsonOptions, cancellationToken);
            return errorResponse?.Message;
        }
        catch
        {
            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.BadRequest => "Invalid reset data.",
                System.Net.HttpStatusCode.NotFound => "Invalid or expired reset link.",
                _ => "Failed to reset password."
            };
        }
    }

    private async Task<string> GetRegistrationErrorFast(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();

            // Quick check for common error patterns
            if (content.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                return "A user with this email already exists.";

            if (content.Contains("invitation", StringComparison.OrdinalIgnoreCase) &&
                content.Contains("invalid", StringComparison.OrdinalIgnoreCase))
                return "Invalid or expired invitation.";

            if (content.Contains("username", StringComparison.OrdinalIgnoreCase) &&
                content.Contains("taken", StringComparison.OrdinalIgnoreCase))
                return "Username is already taken.";

            // Structured parsing fallback
            var errorResponse = JsonSerializer.Deserialize<AuthErrorResponse>(content, _jsonOptions);
            if (errorResponse != null)
            {
                var message = errorResponse.Message;
                if (errorResponse.Errors != null && errorResponse.Errors.Any())
                {
                    message += " " + string.Join(" ", errorResponse.Errors.Take(2));
                }
                return message;
            }

            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.BadRequest => "Invalid registration data.",
                System.Net.HttpStatusCode.Conflict => "User already exists.",
                System.Net.HttpStatusCode.NotFound => "Invalid invitation.",
                _ => "Registration failed."
            };
        }
        catch
        {
            return "Registration failed. Please try again.";
        }
    }

    #endregion

    #region Response Helper Classes

    private sealed class AuthErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public IEnumerable<string>? Errors { get; set; }
    }

    public sealed class ApiException : Exception
    {
        public int StatusCode { get; }
        public string? ErrorCode { get; }

        public ApiException(string message, int statusCode, string? errorCode = null) : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
    }

    #endregion
}