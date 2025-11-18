using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portal.Pages
{
    public class UnauthorizedModel : PageModel
    {
        private readonly ILogger<UnauthorizedModel> _logger;

        public UnauthorizedModel(ILogger<UnauthorizedModel> logger)
        {
            _logger = logger;
        }

        public bool IsDeactivated { get; private set; }
        public string? Message { get; private set; }

        public void OnGet(bool isDeactivated = false, string? message = null)
        {
            IsDeactivated = isDeactivated;
            Message = message;

            if (isDeactivated)
            {
                _logger.LogWarning("Deactivated account access attempt from IP: {IP}", HttpContext.Connection.RemoteIpAddress);
            }
            else
            {
                _logger.LogWarning("Unauthorized access attempt from IP: {IP}", HttpContext.Connection.RemoteIpAddress);
            }
        }
    }
}