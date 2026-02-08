//using Microsoft.AspNetCore.Authentication;
//using Microsoft.AspNetCore.Authentication.Cookies;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.RazorPages;
//using Portal.Services;
//using Shared.DTOs;
//using System.ComponentModel.DataAnnotations;
//using System.IdentityModel.Tokens.Jwt;
//using System.Security.Claims;
//using static Portal.Services.ApiAuthService;

//namespace Portal.Pages.Account
//{
//    public class LoginModel : PageModel
//    {
//        private readonly IApiAuthService _authService;
//        private readonly ILogger<LoginModel> _logger;
//        private static readonly JwtSecurityTokenHandler _tokenHandler = new();

//        public LoginModel(IApiAuthService authService, ILogger<LoginModel> logger)
//        {
//            _authService = authService;
//            _logger = logger;
//        }

//        [BindProperty]
//        public required InputModel Input { get; set; }

//        public string? ErrorMessage { get; set; }

//        public class InputModel
//        {
//            [Required, EmailAddress]
//            public string Email { get; set; } = string.Empty;

//            [Required, DataType(DataType.Password)]
//            public string Password { get; set; } = string.Empty;

//            public bool RememberMe { get; set; }
//        }

//        public void OnGet(string? returnUrl = null) => ViewData["ReturnUrl"] = returnUrl;

//        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
//        {
//            if (!ModelState.IsValid)
//            {
//                ErrorMessage = "Please correct the errors and try again.";
//                return Page();
//            }

//            try
//            {
//                var loginDto = new LoginDTO(Input.Email, Input.Password);
//                var tokenResponse = await _authService.LoginAsync(loginDto);

//                if (tokenResponse == null)
//                {
//                    ErrorMessage = "Invalid email or password.";
//                    return Page();
//                }

//                var (userEmail, roles) = ExtractUserInfoUltraFast(tokenResponse.AccessToken);
//                await SignInUserOptimized(userEmail, roles, tokenResponse, Input.RememberMe);

//                return RedirectToLocal(returnUrl);
//            }
//            catch (ApiException ex) when (ex.StatusCode == 401 && ex.ErrorCode == "ACCOUNT_DEACTIVATED")
//            {
//                return RedirectToPage("/Unauthorized", new { isDeactivated = true });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Login failed for {Email}", Input.Email);
//                ErrorMessage = "Login failed. Please try again.";
//                return Page();
//            }
//        }

//        private async Task SignInUserOptimized(string email, List<string> roles, TokenResponseDTO tokens, bool rememberMe)
//        {
//            var claims = new List<Claim>(roles.Count + 4)
//            {
//                new(ClaimTypes.Name, email),
//                new(ClaimTypes.Email, email),
//                new("AccessToken", tokens.AccessToken),
//                new("RefreshToken", tokens.RefreshToken)
//            };

//            // Pre-allocated role claims
//            foreach (var role in roles)
//            {
//                claims.Add(new(ClaimTypes.Role, role));
//            }

//            await HttpContext.SignInAsync(
//                "PortalCookie",
//                new ClaimsPrincipal(new ClaimsIdentity(claims, "PortalCookie")),
//                new AuthenticationProperties
//                {
//                    IsPersistent = rememberMe,
//                    ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
//                });
//        }

//        private static (string email, List<string> roles) ExtractUserInfoUltraFast(string token)
//        {
//            try
//            {
//                // Ultra-fast token processing with spans
//                ReadOnlySpan<char> tokenSpan = token;
//                if (tokenSpan.StartsWith("Bearer "))
//                {
//                    tokenSpan = tokenSpan.Slice(7);
//                }

//                var jwtToken = _tokenHandler.ReadJwtToken(tokenSpan.ToString());

//                string? email = null;
//                var roles = new List<string>();

//                // Minimal allocation claim processing
//                foreach (var claim in jwtToken.Claims)
//                {
//                    var claimType = claim.Type;

//                    if (email == null)
//                    {
//                        if (claimType == "email" || claimType == ClaimTypes.Email)
//                        {
//                            email = claim.Value;
//                            continue;
//                        }
//                    }

//                    if (claimType == "role" || claimType == ClaimTypes.Role)
//                    {
//                        roles.Add(claim.Value);
//                    }
//                }

//                return (email ?? "unknown", roles);
//            }
//            catch
//            {
//                return ("unknown", new List<string>());
//            }
//        }

//        private IActionResult RedirectToLocal(string? returnUrl)
//        {
//            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToPage("/Index");
//        }
//    }
//}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Services;
using Shared.DTOs;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using static Portal.Services.ApiAuthService;

namespace Portal.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IApiAuthService _authService;
        private readonly ILogger<LoginModel> _logger;
        private static readonly JwtSecurityTokenHandler _tokenHandler = new();

        public LoginModel(IApiAuthService authService, ILogger<LoginModel> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [BindProperty]
        public required InputModel Input { get; set; }

        public string? ErrorMessage { get; set; }
        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email address")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            public bool RememberMe { get; set; }
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ViewData["ReturnUrl"] = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please correct the errors and try again.";
                return Page();
            }

            try
            {
                _logger.LogInformation("Login attempt for {Email}", Input.Email);

                var loginDto = new LoginDTO(Input.Email, Input.Password);
                var tokenResponse = await _authService.LoginAsync(loginDto);

                if (tokenResponse == null)
                {
                    _logger.LogWarning("Login failed for {Email} - Invalid credentials", Input.Email);
                    ErrorMessage = "Invalid email or password.";
                    return Page();
                }

                // Verify we received valid tokens
                if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken) ||
                    string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
                {
                    _logger.LogError("Login returned null/empty tokens for {Email}", Input.Email);
                    ErrorMessage = "Authentication failed. Please try again.";
                    return Page();
                }

                // Extract user info from token
                var (userEmail, roles) = ExtractUserInfo(tokenResponse.AccessToken);

                _logger.LogInformation("Login successful for {Email} with roles: {Roles}",
                    userEmail, string.Join(", ", roles));

                // Sign in user with tokens stored in cookie
                await SignInUserAsync(userEmail, roles, tokenResponse, Input.RememberMe);

                // Small delay to ensure cookie is properly set before redirect
                await Task.Delay(100);

                _logger.LogInformation("User {Email} signed in successfully", userEmail);

                return RedirectToLocal(returnUrl);
            }
            catch (ApiException ex) when (ex.StatusCode == 401 && ex.ErrorCode == "ACCOUNT_DEACTIVATED")
            {
                _logger.LogWarning("Login attempt for deactivated account: {Email}", Input.Email);
                return RedirectToPage("/Unauthorized", new { isDeactivated = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for {Email}", Input.Email);
                ErrorMessage = "An unexpected error occurred. Please try again.";
                return Page();
            }
        }

        private async Task SignInUserAsync(
            string email,
            List<string> roles,
            TokenResponseDTO tokens,
            bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, email),
                new(ClaimTypes.Email, email),
                new("AccessToken", tokens.AccessToken),
                new("RefreshToken", tokens.RefreshToken)
            };

            // Add role claims
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var identity = new ClaimsIdentity(claims, "PortalCookie");
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8),
                AllowRefresh = true,
                IssuedUtc = DateTimeOffset.UtcNow
            };

            await HttpContext.SignInAsync("PortalCookie", principal, authProperties);
            HttpContext.User = principal;

            _logger.LogInformation("Authentication cookie created for {Email} (RememberMe: {RememberMe})",
                email, rememberMe);
        }

        private (string email, List<string> roles) ExtractUserInfo(string token)
        {
            try
            {
                // Remove "Bearer " prefix if present
                if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = token.Substring(7);
                }

                var jwtToken = _tokenHandler.ReadJwtToken(token);

                string? email = null;
                var roles = new List<string>();

                foreach (var claim in jwtToken.Claims)
                {
                    if (claim.Type == "email" || claim.Type == ClaimTypes.Email)
                    {
                        email = claim.Value;
                    }
                    else if (claim.Type == "role" || claim.Type == ClaimTypes.Role)
                    {
                        roles.Add(claim.Value);
                    }
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    // Fallback to other possible email claims
                    email = jwtToken.Claims
                        .FirstOrDefault(c => c.Type.Contains("email", StringComparison.OrdinalIgnoreCase))
                        ?.Value ?? "unknown@user.com";
                }

                _logger.LogDebug("Extracted from token - Email: {Email}, Roles: {Roles}",
                    email, string.Join(", ", roles));

                return (email, roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract user info from token");
                return ("unknown@user.com", new List<string>());
            }
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                _logger.LogDebug("Redirecting to return URL: {ReturnUrl}", returnUrl);
                return Redirect(returnUrl);
            }

            _logger.LogDebug("Redirecting to default page (Index)");
            return RedirectToPage("/Index");

            //// Don't redirect directly - go through landing page to ensure cookie is set
            //if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            //{
            //    _logger.LogDebug("Redirecting to landing page with return URL: {ReturnUrl}", returnUrl);
            //    return RedirectToPage("/LoginRedirect", new { returnUrl });
            //}

            //_logger.LogDebug("Redirecting to landing page with default target (Index)");
            //return RedirectToPage("/LoginRedirect", new { returnUrl = "/Diagnostics/TokenDiagnostics" });
        }
    }
}