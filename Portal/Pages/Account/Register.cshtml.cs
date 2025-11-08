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
    public class RegisterModel : PageModel
    {
        private readonly IApiAuthService _authService;

        public RegisterModel(IApiAuthService authService)
        {
            _authService = authService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Username is required.")]
            [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
            [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores.")]
            public string Username { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email address.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "First name is required.")]
            [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
            public string FirstName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Surname is required.")]
            [StringLength(50, ErrorMessage = "Surname cannot exceed 50 characters.")]
            public string Surname { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required.")]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Confirm password is required.")]
            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.Exception?.Message : e.ErrorMessage)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .ToArray();

                ErrorMessage = errors.Length == 0 ? "Form validation failed." : string.Join(" ", errors);
                return Page();
            }

            try
            {
                // Create RegisterDTO from input
                var registerDto = new RegisterDTO(
                    Input.Username,
                    Input.Email,
                    Input.Password,
                    Input.FirstName,
                    Input.Surname
                );

                // Call the API to register
                var result = await _authService.RegisterAsync(registerDto);
                if (!result)
                {
                    ErrorMessage = "Registration failed. Please try again.";
                    return Page();
                }

                SuccessMessage = "Registration successful! You can now sign in.";

                // Clear form
                Input = new InputModel();

                // Optionally auto-login after registration
                // return await AutoLoginAfterRegistration(registerDto);

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred during registration. Please try again.";
                // Log the exception in a real application
                return Page();
            }
        }

        // Optional: Auto-login after successful registration
        private async Task<IActionResult> AutoLoginAfterRegistration(RegisterDTO registerDto)
        {
            var loginDto = new LoginDTO(registerDto.Email, registerDto.Password);
            var tokenResponse = await _authService.LoginAsync(loginDto);

            if (tokenResponse == null)
            {
                SuccessMessage = "Registration successful! Please sign in.";
                return Page();
            }

            // Create claims and sign in
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, registerDto.Email),
                new Claim("AccessToken", tokenResponse.AccessToken),
                new Claim("RefreshToken", tokenResponse.RefreshToken)
            };

            var claimsIdentity = new ClaimsIdentity(claims, "PortalCookie");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                "PortalCookie",
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            return RedirectToPage("/Index");
        }
    }
}