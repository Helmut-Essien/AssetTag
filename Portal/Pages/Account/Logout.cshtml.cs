using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication;

namespace Portal.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly IApiAuthService _authService;
        private readonly ILogger<LogoutModel> _logger;
        private const string CookieScheme = "PortalCookie";

        public LogoutModel(IApiAuthService authService, ILogger<LogoutModel> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        public void OnGet()
        {
            // Render page (POST triggers actual logout)
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                var refreshToken = User?.FindFirst("RefreshToken")?.Value;
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    // Best-effort revoke at the API
                    await _authService.RevokeAsync(refreshToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while revoking refresh token during logout for user {User}.", User?.Identity?.Name);
                // Continue with sign-out even if revoke fails
            }

            // Sign out of the portal cookie
            await HttpContext.SignOutAsync(CookieScheme);

            // Redirect to login page
            return RedirectToPage("/Account/Login");
        }
    }
}