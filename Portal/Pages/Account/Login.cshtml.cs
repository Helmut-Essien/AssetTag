using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

        public class InputModel
        {
            [Required, EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required, DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            public bool RememberMe { get; set; }
        }

        public void OnGet(string? returnUrl = null) => ViewData["ReturnUrl"] = returnUrl;

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please correct the errors and try again.";
                return Page();
            }

            try
            {
                var loginDto = new LoginDTO(Input.Email, Input.Password);
                var tokenResponse = await _authService.LoginAsync(loginDto);

                if (tokenResponse == null)
                {
                    ErrorMessage = "Invalid email or password.";
                    return Page();
                }

                var (userEmail, roles) = ExtractUserInfoUltraFast(tokenResponse.AccessToken);
                await SignInUserOptimized(userEmail, roles, tokenResponse, Input.RememberMe);

                return RedirectToLocal(returnUrl);
            }
            catch (ApiException ex) when (ex.StatusCode == 401 && ex.ErrorCode == "ACCOUNT_DEACTIVATED")
            {
                return RedirectToPage("/Unauthorized", new { isDeactivated = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for {Email}", Input.Email);
                ErrorMessage = "Login failed. Please try again.";
                return Page();
            }
        }

        private async Task SignInUserOptimized(string email, List<string> roles, TokenResponseDTO tokens, bool rememberMe)
        {
            var claims = new List<Claim>(roles.Count + 4)
            {
                new(ClaimTypes.Name, email),
                new(ClaimTypes.Email, email),
                new("AccessToken", tokens.AccessToken),
                new("RefreshToken", tokens.RefreshToken)
            };

            // Pre-allocated role claims
            foreach (var role in roles)
            {
                claims.Add(new(ClaimTypes.Role, role));
            }

            await HttpContext.SignInAsync(
                "PortalCookie",
                new ClaimsPrincipal(new ClaimsIdentity(claims, "PortalCookie")),
                new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
                });
        }

        private static (string email, List<string> roles) ExtractUserInfoUltraFast(string token)
        {
            try
            {
                // Ultra-fast token processing with spans
                ReadOnlySpan<char> tokenSpan = token;
                if (tokenSpan.StartsWith("Bearer "))
                {
                    tokenSpan = tokenSpan.Slice(7);
                }

                var jwtToken = _tokenHandler.ReadJwtToken(tokenSpan.ToString());

                string? email = null;
                var roles = new List<string>();

                // Minimal allocation claim processing
                foreach (var claim in jwtToken.Claims)
                {
                    var claimType = claim.Type;

                    if (email == null)
                    {
                        if (claimType == "email" || claimType == ClaimTypes.Email)
                        {
                            email = claim.Value;
                            continue;
                        }
                    }

                    if (claimType == "role" || claimType == ClaimTypes.Role)
                    {
                        roles.Add(claim.Value);
                    }
                }

                return (email ?? "unknown", roles);
            }
            catch
            {
                return ("unknown", new List<string>());
            }
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToPage("/Index");
        }
    }
}