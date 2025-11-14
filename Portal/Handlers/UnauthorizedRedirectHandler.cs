using Microsoft.AspNetCore.Http;
using System.Net;

namespace Portal.Handlers
{
    public class UnauthorizedRedirectHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<UnauthorizedRedirectHandler> _logger;

        public UnauthorizedRedirectHandler(IHttpContextAccessor httpContextAccessor, ILogger<UnauthorizedRedirectHandler> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("API returned {StatusCode} for request to {RequestUri}", response.StatusCode, request.RequestUri);

                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null && !httpContext.Response.HasStarted)
                {
                    var returnUrl = httpContext.Request.Path + httpContext.Request.QueryString;
                    var redirectUrl = response.StatusCode == HttpStatusCode.Unauthorized
                        ? $"/Unauthorized?returnUrl={Uri.EscapeDataString(returnUrl)}"
                        : $"/Forbidden?returnUrl={Uri.EscapeDataString(returnUrl)}";

                    httpContext.Response.Redirect(redirectUrl);
                }
            }

            return response;
        }
    }
}