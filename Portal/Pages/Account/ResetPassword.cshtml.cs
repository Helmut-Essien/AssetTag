using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Services;
using Shared.DTOs;
using System.ComponentModel.DataAnnotations;

namespace Portal.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly IApiAuthService _authService;
        private readonly ILogger<ResetPasswordModel> _logger;

        public ResetPasswordModel(IApiAuthService authService, ILogger<ResetPasswordModel> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsSuccess { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email address.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Token is required.")]
            public string Token { get; set; } = string.Empty;

            [Required(ErrorMessage = "New password is required.")]
            [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
            [DataType(DataType.Password)]
            public string NewPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please confirm your password.")]
            [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
            [DataType(DataType.Password)]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public IActionResult OnGet(string? email = null, string? token = null)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                ErrorMessage = "Invalid reset link. Please request a new password reset link.";
                return Page();
            }

            Input.Email = email;
            Input.Token = Uri.UnescapeDataString(token);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var resetPasswordDto = new ResetPasswordDTO
                {
                    Email = Input.Email,
                    Token = Input.Token,
                    NewPassword = Input.NewPassword,
                    ConfirmPassword = Input.ConfirmPassword
                };

                var result = await _authService.ResetPasswordAsync(resetPasswordDto);

                if (result != null && result.Success)
                {
                    SuccessMessage = result.Message ?? "Password has been reset successfully. You can now login with your new password.";
                    IsSuccess = true;

                    // Clear the form
                    ModelState.Clear();
                    Input = new InputModel();
                }
                else
                {
                    ErrorMessage = result?.Message ?? "Failed to reset password. The link may have expired. Please request a new reset link.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during password reset for {Email}", Input.Email);
                ErrorMessage = "An error occurred while resetting your password. Please try again.";
            }

            return Page();
        }
    }
}