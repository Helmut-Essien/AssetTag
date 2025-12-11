using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Portal.Pages.Diagnostics;

public class TimeCheckModel : PageModel
{
    private readonly HttpClient _http;
    private readonly ILogger<TimeCheckModel> _logger;

    public DateTime PortalUtc { get; private set; }
    public DateTime ApiUtc { get; private set; }
    public TimeSpan Skew { get; private set; }
    public string? Error { get; private set; }

    public TimeCheckModel(IHttpClientFactory httpFactory, ILogger<TimeCheckModel> logger)
    {
        _http = httpFactory.CreateClient("AssetTagApi");
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        PortalUtc = DateTime.UtcNow;

        try
        {
            var dto = await _http.GetFromJsonAsync<ServerTimeDto>("api/diagnostics/server-time");
            if (dto == null)
            {
                Error = "No response from API.";
                _logger.LogWarning("TimeCheck: API returned null.");
                return;
            }

            ApiUtc = dto.serverUtc;
            Skew = ApiUtc - PortalUtc;

            _logger.LogInformation("TimeCheck: portalUtc={PortalUtc:o}, apiUtc={ApiUtc:o}, skewSeconds={SkewSeconds}",
                PortalUtc, ApiUtc, Skew.TotalSeconds);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            _logger.LogError(ex, "TimeCheck failed calling API server-time");
        }
    }

    private record ServerTimeDto(DateTime serverUtc);
}
