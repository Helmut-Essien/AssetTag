using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Services;
using Shared.DTOs;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Portal.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IApiAuthService _authService;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(IApiAuthService authService, ILogger<LoginModel> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email address.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            public bool RememberMe { get; set; }
        }

        public void OnGet(string? returnUrl = null)
        {
            // Store returnUrl in ViewData for use in the view
            ViewData["ReturnUrl"] = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                // Collect model state errors to display so you can see why validation failed
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.Exception?.Message : e.ErrorMessage)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .ToArray();

                ErrorMessage = errors.Length == 0 ? "Form validation failed." : string.Join(" ", errors);
                return Page();
            }

            // Create LoginDTO from input
            var loginDto = new LoginDTO(Input.Email, Input.Password);

            // Call the API to authenticate
            var tokenResponse = await _authService.LoginAsync(loginDto);
            if (tokenResponse == null)
            {
                ErrorMessage = "Invalid email or password.";
                return Page();
            }








            // Extract user information and roles from the JWT token
            var (userEmail, roles) = ExtractUserInfoFromToken(tokenResponse.AccessToken);

            _logger.LogInformation("User {Email} logged in with roles: {Roles}",
                userEmail, string.Join(", ", roles));

            // Create claims from the JWT token data
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userEmail),
                new Claim(ClaimTypes.Email, userEmail),
                new Claim("AccessToken", tokenResponse.AccessToken), // Store token as claim if needed
                new Claim("RefreshToken", tokenResponse.RefreshToken) // Store refresh token as claim
            };

            // Add role claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }












            // Create claims identity
            var claimsIdentity = new ClaimsIdentity(claims, "PortalCookie");

            // Set authentication properties
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = Input.RememberMe,
                ExpiresUtc = Input.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
            };

            // Sign in the user using cookie authentication
            await HttpContext.SignInAsync(
                "PortalCookie",
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            
            return RedirectToPage("/Index");
        }













        private (string email, List<string> roles) ExtractUserInfoFromToken(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();

                if (token.StartsWith("Bearer "))
                {
                    token = token.Substring(7);
                }

                var jwtToken = handler.ReadJwtToken(token);

                var email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value
                          ?? "unknown";

                var roles = jwtToken.Claims
                    .Where(c => c.Type == "role" || c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                return (email, roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting user info from token");
                return (Input.Email, new List<string>());
            }
        }
    }
}