using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Services;
using Shared.DTOs;
using System.ComponentModel.DataAnnotations;

namespace Portal.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly IApiAuthService _authService;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(IApiAuthService authService, ILogger<ForgotPasswordModel> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ResetToken { get; set; }
        public bool ShowResetToken { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email address.")]
            public string Email { get; set; } = string.Empty;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var forgotPasswordDto = new ForgotPasswordDTO { Email = Input.Email };
                var result = await _authService.ForgotPasswordAsync(forgotPasswordDto);

                if (result != null)
                {
                    // The API now handles email sending, we just show success message
                    SuccessMessage = "If the email exists, a password reset link has been sent to your email address.";

                    //// For development/testing - remove in production
                    //if (!string.IsNullOrEmpty(result.ResetToken))
                    //{
                    //    ResetToken = result.ResetToken;
                    //    ShowResetToken = true;
                    //    _logger.LogInformation("Password reset token for {Email}: {Token}", Input.Email, result.ResetToken);
                    //}
                }
                else
                {
                    // Same message when API fails — do not reveal whether the email exists
                    SuccessMessage = "If the email exists, a password reset link has been sent to your email address.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during forgot password for {Email}", Input.Email);
                SuccessMessage = "If the email exists, a password reset link has been sent to your email address.";
            }

            // Clear the form
            ModelState.Clear();
            Input = new InputModel();

            return Page();
        }
    }
}