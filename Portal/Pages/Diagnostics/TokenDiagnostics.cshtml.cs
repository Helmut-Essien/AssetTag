using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Portal.Pages.Diagnostics;

[Authorize]
public class TokenDiagnosticsModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TokenDiagnosticsModel> _logger;
    private readonly IConfiguration _configuration;

    public TokenDiagnosticsModel(
        IHttpClientFactory httpClientFactory,
        ILogger<TokenDiagnosticsModel> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient("AssetTagApi");
        _logger = logger;
        _configuration = configuration;
    }

    public bool IsAuthenticated { get; set; }
    public string? RawToken { get; set; }
    public string? TruncatedToken { get; set; }
    public TokenValidationResult? ValidationResult { get; set; }
    public List<TokenClaim>? TokenClaims { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CheckTime { get; set; }
    public DateTime? TokenExpiryTime { get; set; }
    public TimeSpan? TimeToExpiry { get; set; }
    public string? ValidationEndpoint { get; set; }
    public string? ApiBaseUrl { get; set; }
    public string? UserId { get; set; }
    public string? CorrelationId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        CorrelationId = Guid.NewGuid().ToString();

        _logger.LogInformation("🔍 Token Diagnostics started - CorrelationId: {CorrelationId}, IP: {RemoteIp}, UserAgent: {UserAgent}",
            CorrelationId,
            HttpContext.Connection.RemoteIpAddress,
            Request.Headers.UserAgent);

        CheckTime = DateTime.UtcNow;
        IsAuthenticated = User.Identity?.IsAuthenticated ?? false;
        ApiBaseUrl = _configuration["ApiBaseUrl"] ?? _httpClient.BaseAddress?.ToString();
        ValidationEndpoint = $"{ApiBaseUrl?.TrimEnd('/')}/api/auth/validate-token";

        _logger.LogDebug("Diagnostics Configuration - BaseUrl: {ApiBaseUrl}, Endpoint: {ValidationEndpoint}, Authenticated: {IsAuthenticated}",
            ApiBaseUrl, ValidationEndpoint, IsAuthenticated);

        if (!IsAuthenticated)
        {
            _logger.LogWarning("Token diagnostics accessed without authentication - CorrelationId: {CorrelationId}", CorrelationId);
            ErrorMessage = "You must be logged in to diagnose tokens.";
            return Page();
        }

        try
        {
            // Get user information
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;

            _logger.LogInformation("Token diagnostics for user {UserId} - CorrelationId: {CorrelationId}",
                UserId, CorrelationId);

            // Get token from authentication
            var token = User.FindFirst("AccessToken")?.Value;
            if (string.IsNullOrEmpty(token))
            {
                // Try to get from Authorization header
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader?.StartsWith("Bearer ") == true)
                {
                    token = authHeader.Substring("Bearer ".Length).Trim();
                    _logger.LogDebug("Token retrieved from Authorization header - CorrelationId: {CorrelationId}", CorrelationId);
                }
                else
                {
                    _logger.LogDebug("Token not found in Authorization header - CorrelationId: {CorrelationId}", CorrelationId);
                }
            }
            else
            {
                _logger.LogDebug("Token retrieved from authentication cookie - CorrelationId: {CorrelationId}", CorrelationId);
            }

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No access token found for authenticated user {UserId} - CorrelationId: {CorrelationId}",
                    UserId, CorrelationId);
                ErrorMessage = "No access token found. Please log in again.";
                return Page();
            }

            RawToken = token;
            TruncatedToken = TruncateToken(token);

            _logger.LogDebug("Token retrieved - Length: {TokenLength}, Truncated: {TruncatedToken}, CorrelationId: {CorrelationId}",
                token.Length, TruncatedToken, CorrelationId);

            // Parse token claims
            var parseStopwatch = Stopwatch.StartNew();
            TokenClaims = ParseTokenClaims(token);
            parseStopwatch.Stop();
            _logger.LogDebug("Token parsed in {ParseTime}ms - {ClaimCount} claims found, CorrelationId: {CorrelationId}",
                parseStopwatch.ElapsedMilliseconds, TokenClaims?.Count, CorrelationId);

            // Log important claims
            LogImportantClaims(token);

            // Call API to validate token
            var validationStopwatch = Stopwatch.StartNew();
            await ValidateTokenWithApi(token);
            validationStopwatch.Stop();

            _logger.LogInformation("Token validation completed in {ValidationTime}ms - IsValid: {IsValid}, CorrelationId: {CorrelationId}",
                validationStopwatch.ElapsedMilliseconds, ValidationResult?.IsValid, CorrelationId);

            // Check token expiry
            if (ValidationResult?.ExpiresAt != null)
            {
                TokenExpiryTime = ValidationResult.ExpiresAt;
                TimeToExpiry = TokenExpiryTime - CheckTime;

                _logger.LogDebug("Token expiry calculated - ExpiresAt: {ExpiresAt}, TimeToExpiry: {TimeToExpiry}, CorrelationId: {CorrelationId}",
                    TokenExpiryTime, TimeToExpiry, CorrelationId);
            }

            stopwatch.Stop();

            // Log comprehensive summary
            LogDiagnosticsSummary(stopwatch.ElapsedMilliseconds);

            return Page();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "❌ Token diagnostics error for user {UserId} - Elapsed: {ElapsedMs}ms, CorrelationId: {CorrelationId}",
                UserId, stopwatch.ElapsedMilliseconds, CorrelationId);

            ErrorMessage = $"Error analyzing token: {ex.Message}";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostValidateTokenAsync([FromForm] string? customToken = null)
    {
        var stopwatch = Stopwatch.StartNew();
        CorrelationId = Guid.NewGuid().ToString();
        CheckTime = DateTime.UtcNow;

        _logger.LogInformation("🔄 Token validation POST started - CorrelationId: {CorrelationId}, HasCustomToken: {HasCustomToken}",
            CorrelationId, !string.IsNullOrEmpty(customToken));

        if (!string.IsNullOrEmpty(customToken))
        {
            _logger.LogInformation("Validating custom token - Length: {TokenLength}, CorrelationId: {CorrelationId}",
                customToken.Length, CorrelationId);

            // Validate custom token entered by user
            RawToken = customToken;
            TruncatedToken = TruncateToken(customToken);

            _logger.LogDebug("Custom token truncated - Original: {TokenLength}, Truncated: {TruncatedToken}, CorrelationId: {CorrelationId}",
                customToken.Length, TruncatedToken, CorrelationId);

            var parseStopwatch = Stopwatch.StartNew();
            TokenClaims = ParseTokenClaims(customToken);
            parseStopwatch.Stop();

            _logger.LogDebug("Custom token parsed in {ParseTime}ms - {ClaimCount} claims, CorrelationId: {CorrelationId}",
                parseStopwatch.ElapsedMilliseconds, TokenClaims?.Count, CorrelationId);

            try
            {
                var validationStopwatch = Stopwatch.StartNew();
                await ValidateTokenWithApi(customToken);
                validationStopwatch.Stop();

                _logger.LogInformation("Custom token validation completed in {ValidationTime}ms - IsValid: {IsValid}, CorrelationId: {CorrelationId}",
                    validationStopwatch.ElapsedMilliseconds, ValidationResult?.IsValid, CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Custom token validation failed - CorrelationId: {CorrelationId}", CorrelationId);
                ValidationResult = new TokenValidationResult
                {
                    IsValid = false,
                    Message = "Validation failed",
                    DetailedMessage = ex.Message
                };
            }
        }
        else
        {
            _logger.LogDebug("No custom token provided, re-validating current token - CorrelationId: {CorrelationId}", CorrelationId);
            // Re-validate current token
            return await OnGetAsync();
        }

        stopwatch.Stop();
        _logger.LogInformation("Token validation POST completed in {TotalTime}ms - CorrelationId: {CorrelationId}",
            stopwatch.ElapsedMilliseconds, CorrelationId);

        return Page();
    }

    private async Task ValidateTokenWithApi(string token)
    {
        _logger.LogDebug("Starting API token validation - CorrelationId: {CorrelationId}", CorrelationId);

        try
        {
            // Set authorization header
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _logger.LogTrace("Authorization header set - CorrelationId: {CorrelationId}", CorrelationId);

            var requestStopwatch = Stopwatch.StartNew();
            var response = await _httpClient.PostAsync("api/auth/validate-token", null);
            requestStopwatch.Stop();

            _logger.LogDebug("API validation request completed in {RequestTime}ms - Status: {StatusCode}, CorrelationId: {CorrelationId}",
                requestStopwatch.ElapsedMilliseconds, response.StatusCode, CorrelationId);

            var responseBody = await response.Content.ReadAsStringAsync();

            // Log response (truncated for sensitive data)
            var safeResponse = responseBody.Length > 500 ? responseBody.Substring(0, 500) + "..." : responseBody;
            _logger.LogTrace("API Response - Status: {StatusCode}, Body: {ResponseBody}, CorrelationId: {CorrelationId}",
                response.StatusCode, safeResponse, CorrelationId);

            if (response.IsSuccessStatusCode)
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                try
                {
                    var validationResponse = JsonSerializer.Deserialize<ApiValidationResponse>(responseBody, options);
                    _logger.LogDebug("Response deserialized successfully - IsValid: {IsValid}, CorrelationId: {CorrelationId}",
                        validationResponse?.IsValid, CorrelationId);

                    if (validationResponse?.IsValid == true)
                    {
                        ValidationResult = new TokenValidationResult
                        {
                            IsValid = true,
                            UserId = validationResponse.UserId,
                            UserName = validationResponse.UserName,
                            Email = validationResponse.Email,
                            Roles = validationResponse.Roles,
                            ExpiresAt = validationResponse.ExpiresAt,
                            IssuedAt = validationResponse.IssuedAt,
                            Message = "Token is valid",
                            DetailedMessage = "API validation successful"
                        };

                        _logger.LogInformation("✅ Token validation SUCCESS - UserId: {UserId}, Roles: {Roles}, CorrelationId: {CorrelationId}",
                            validationResponse.UserId,
                            validationResponse.Roles != null ? string.Join(",", validationResponse.Roles) : "none",
                            CorrelationId);
                    }
                    else
                    {
                        ValidationResult = new TokenValidationResult
                        {
                            IsValid = false,
                            Message = validationResponse?.Message ?? "Token is invalid",
                            DetailedMessage = "API returned invalid status"
                        };

                        _logger.LogWarning("Token validation returned invalid - Message: {Message}, CorrelationId: {CorrelationId}",
                            validationResponse?.Message, CorrelationId);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "JSON deserialization error - Response: {ResponseBody}, CorrelationId: {CorrelationId}",
                        safeResponse, CorrelationId);

                    // Try to parse as error response
                    var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(responseBody, options);
                    ValidationResult = new TokenValidationResult
                    {
                        IsValid = false,
                        Message = errorResponse?.Message ?? "Invalid response format",
                        DetailedMessage = errorResponse?.Details ?? $"JSON parsing failed: {jsonEx.Message}"
                    };
                }
            }
            else
            {
                _logger.LogWarning("API returned error status - StatusCode: {StatusCode}, Body: {ResponseBody}, CorrelationId: {CorrelationId}",
                    response.StatusCode, safeResponse, CorrelationId);

                ValidationResult = new TokenValidationResult
                {
                    IsValid = false,
                    Message = $"HTTP {response.StatusCode}",
                    DetailedMessage = responseBody
                };
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "HTTP request failed - CorrelationId: {CorrelationId}", CorrelationId);
            ValidationResult = new TokenValidationResult
            {
                IsValid = false,
                Message = "API request failed",
                DetailedMessage = httpEx.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token validation - CorrelationId: {CorrelationId}", CorrelationId);
            ValidationResult = new TokenValidationResult
            {
                IsValid = false,
                Message = "Validation error",
                DetailedMessage = ex.Message
            };
        }
    }

    private List<TokenClaim> ParseTokenClaims(string token)
    {
        var claims = new List<TokenClaim>();

        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);

                // Log token header
                _logger.LogTrace("JWT Header - Algorithm: {Algorithm}, Type: {Type}, CorrelationId: {CorrelationId}",
                    jwtToken.Header.Alg, jwtToken.Header.Typ, CorrelationId);

                // Log token signature (first few chars only)
                var signaturePreview = jwtToken.RawSignature?.Length > 10
                    ? jwtToken.RawSignature.Substring(0, 10) + "..."
                    : jwtToken.RawSignature;
                _logger.LogTrace("JWT Signature Preview: {SignaturePreview}, CorrelationId: {CorrelationId}",
                    signaturePreview, CorrelationId);

                foreach (var claim in jwtToken.Claims)
                {
                    claims.Add(new TokenClaim
                    {
                        Type = claim.Type,
                        Value = claim.Value.Length > 100
                            ? claim.Value.Substring(0, 100) + "..."
                            : claim.Value,
                        Description = GetClaimDescription(claim.Type)
                    });
                }
            }
            else
            {
                _logger.LogWarning("Cannot read token as JWT - CorrelationId: {CorrelationId}", CorrelationId);
                claims.Add(new TokenClaim
                {
                    Type = "Error",
                    Value = "Token is not a valid JWT",
                    Description = "Token parsing error"
                });
            }
        }
        catch (ArgumentException argEx)
        {
            _logger.LogError(argEx, "Invalid token format - CorrelationId: {CorrelationId}", CorrelationId);
            claims.Add(new TokenClaim
            {
                Type = "Error",
                Value = $"Invalid token format: {argEx.Message}",
                Description = "Token parsing error"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse token claims - CorrelationId: {CorrelationId}", CorrelationId);
            claims.Add(new TokenClaim
            {
                Type = "Error",
                Value = $"Failed to parse claims: {ex.Message}",
                Description = "Token parsing error"
            });
        }

        return claims;
    }

    private void LogImportantClaims(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);

                var sub = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                var name = jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name" || c.Type == "name")?.Value;
                var email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
                var roles = jwtToken.Claims
                    .Where(c => c.Type == "role" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                    .Select(c => c.Value)
                    .ToList();
                var exp = jwtToken.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
                var iat = jwtToken.Claims.FirstOrDefault(c => c.Type == "iat")?.Value;
                var aud = jwtToken.Claims.FirstOrDefault(c => c.Type == "aud")?.Value;
                var iss = jwtToken.Claims.FirstOrDefault(c => c.Type == "iss")?.Value;

                _logger.LogInformation("📋 Token Claims Summary - Sub: {Sub}, Name: {Name}, Email: {Email}, Roles: {Roles}, Exp: {Exp}, Iat: {Iat}, Aud: {Aud}, Iss: {Iss}, CorrelationId: {CorrelationId}",
                    sub, name, email, roles != null ? string.Join(",", roles) : "none", exp, iat, aud, iss, CorrelationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log important claims - CorrelationId: {CorrelationId}", CorrelationId);
        }
    }

    private void LogDiagnosticsSummary(long totalElapsedMs)
    {
        var summary = new
        {
            CorrelationId,
            UserId,
            IsAuthenticated,
            TokenLength = RawToken?.Length ?? 0,
            ValidationResult = ValidationResult?.IsValid ?? false,
            ValidationMessage = ValidationResult?.Message,
            ClaimsCount = TokenClaims?.Count ?? 0,
            ExpiresAt = TokenExpiryTime?.ToString("o"),
            TimeToExpiry = TimeToExpiry?.ToString(@"hh\:mm\:ss"),
            TotalTimeMs = totalElapsedMs
        };

        _logger.LogInformation("📊 Token Diagnostics Summary: {@Summary}", summary);
    }

    private string GetClaimDescription(string claimType)
    {
        return claimType switch
        {
            "sub" => "Subject (User ID)",
            "unique_name" or "name" => "Username",
            "email" => "Email address",
            "role" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" => "User role",
            "exp" => "Expiration time (timestamp)",
            "iat" => "Issued at (timestamp)",
            "nbf" => "Not before (timestamp)",
            "jti" => "JWT ID",
            "aud" => "Audience",
            "iss" => "Issuer",
            "tid" => "Tenant ID",
            "oid" => "Object ID",
            "upn" => "User Principal Name",
            "preferred_username" => "Preferred Username",
            _ => claimType.Contains("schemas.microsoft.com")
                ? $"Microsoft claim: {claimType.Split('/').Last()}"
                : "Custom claim"
        };
    }

    private string TruncateToken(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length <= 30)
            return token;

        return $"{token.Substring(0, 15)}...{token.Substring(token.Length - 15)}";
    }

    public class TokenValidationResult
    {
        public bool IsValid { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public List<string>? Roles { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? IssuedAt { get; set; }
        public string? Message { get; set; }
        public string? DetailedMessage { get; set; }
    }

    public class TokenClaim
    {
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private class ApiValidationResponse
    {
        public bool IsValid { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public List<string>? Roles { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? IssuedAt { get; set; }
        public string? Message { get; set; }
    }

    private class ApiErrorResponse
    {
        public string? Message { get; set; }
        public string? Details { get; set; }
    }
}