using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portal.Pages
{
    public class LoginRedirectModel : PageModel
    {
        private readonly ILogger<LoginRedirectModel> _logger;

        public LoginRedirectModel(ILogger<LoginRedirectModel> logger)
        {
            _logger = logger;
        }

        public string ReturnUrl { get; set; } =/* "/"*/ "/Diagnostics/TokenDiagnostics";
        public bool HasFreshTokens { get; set; }

        public IActionResult OnGet(string? returnUrl = null)
        {
            // Validate return URL to prevent open redirect attacks
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                ReturnUrl = returnUrl;
            }
            else
            {
                ReturnUrl = "/Diagnostics/TokenDiagnostics";
            }

            // Check if we have fresh tokens from login
            if (HttpContext.Items.TryGetValue("FreshTokens", out var tokensObj) && tokensObj is FreshTokenInfo)
            {
                HasFreshTokens = true;
                _logger.LogInformation("Fresh tokens available for login redirect");
            }
            else
            {
                HasFreshTokens = false;
                _logger.LogWarning("No fresh tokens found for login redirect");
            }

            return Page();
        }
    }

    // Helper class (defined in LoginModel.cshtml.cs)
    public class FreshTokenInfo
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresUtc { get; set; }
    }
}