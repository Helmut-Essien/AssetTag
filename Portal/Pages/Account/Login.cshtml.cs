using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Services;
using Shared.DTOs;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Portal.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IApiAuthService _authService;

        public LoginModel(IApiAuthService authService)
        {
            _authService = authService;
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

            // Create claims for the authenticated user
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, Input.Email),
                new Claim("AccessToken", tokenResponse.AccessToken),
                new Claim("RefreshToken", tokenResponse.RefreshToken)
            };

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

            // Redirect to returnUrl or home page
            returnUrl = returnUrl ?? Url.Content("~/Index.cshtml");
            return LocalRedirect(returnUrl);
        }
    }
}