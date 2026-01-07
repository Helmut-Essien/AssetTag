using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared.DTOs;
using System.Net.Http.Json;

namespace Portal.Pages.Assets
{
    [Authorize]
    public class AssetHistoriesModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AssetHistoriesModel> _logger;

        public AssetHistoriesModel(IHttpClientFactory httpClientFactory, ILogger<AssetHistoriesModel> logger)
        {
            _httpClient = httpClientFactory.CreateClient("AssetTagApi");
            _logger = logger;
        }

        // Properties for the page
        public PaginatedResponse<AssetHistoryReadDTO> Histories { get; set; } = new();
        public AssetHistoryFilters? AvailableFilters { get; set; }

        // Filter properties
        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 20;

        [BindProperty(SupportsGet = true)]
        public string? ActionFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? AssetNameFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? UserNameFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DateFrom { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DateTo { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Load available filters
                await LoadAvailableFilters();

                // Build API URL with query parameters
                var apiUrl = BuildApiUrl();

                // Call API
                var response = await _httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    ModelState.AddModelError("", "Unable to load asset histories. Please try again.");
                    return Page();
                }

                Histories = await response.Content.ReadFromJsonAsync<PaginatedResponse<AssetHistoryReadDTO>>()
                    ?? new PaginatedResponse<AssetHistoryReadDTO>();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading asset histories");
                ModelState.AddModelError("", "An error occurred while loading asset histories.");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostSearchAsync()
        {
            // Reset to first page when searching
            CurrentPage = 1;
            return await OnGetAsync();
        }

        public async Task<IActionResult> OnPostClearFiltersAsync()
        {
            CurrentPage = 1;
            ActionFilter = null;
            AssetNameFilter = null;
            UserNameFilter = null;
            DateFrom = null;
            DateTo = null;
            SearchQuery = null;

            return await OnGetAsync();
        }

        public IActionResult OnGetExportAsync(string format = "json")
        {
            // Build export URL (you'll need to implement an export endpoint)
            var exportUrl = BuildExportUrl(format);
            return Redirect(exportUrl);
        }

        private string BuildApiUrl()
        {
            var baseUrl = string.IsNullOrWhiteSpace(SearchQuery)
                ? "api/assethistories"
                : "api/assethistories/search";

            var queryParams = new List<string>
            {
                $"page={CurrentPage}",
                $"pageSize={PageSize}"
            };

            if (!string.IsNullOrEmpty(SearchQuery))
            {
                queryParams.Add($"query={Uri.EscapeDataString(SearchQuery)}");
            }
            else
            {
                if (!string.IsNullOrEmpty(ActionFilter))
                    queryParams.Add($"action={Uri.EscapeDataString(ActionFilter)}");

                if (!string.IsNullOrEmpty(AssetNameFilter))
                    queryParams.Add($"assetName={Uri.EscapeDataString(AssetNameFilter)}");

                if (!string.IsNullOrEmpty(UserNameFilter))
                    queryParams.Add($"userName={Uri.EscapeDataString(UserNameFilter)}");

                if (!string.IsNullOrEmpty(DateFrom) && DateTime.TryParse(DateFrom, out _))
                    queryParams.Add($"fromDate={DateFrom}");

                if (!string.IsNullOrEmpty(DateTo) && DateTime.TryParse(DateTo, out _))
                    queryParams.Add($"toDate={DateTo}");
            }

            return $"{baseUrl}?{string.Join("&", queryParams)}";
        }

        private string BuildExportUrl(string format)
        {
            var queryParams = new List<string>();

            if (!string.IsNullOrEmpty(ActionFilter))
                queryParams.Add($"action={ActionFilter}");

            if (!string.IsNullOrEmpty(AssetNameFilter))
                queryParams.Add($"assetName={AssetNameFilter}");

            if (!string.IsNullOrEmpty(UserNameFilter))
                queryParams.Add($"userName={UserNameFilter}");

            if (!string.IsNullOrEmpty(DateFrom))
                queryParams.Add($"fromDate={DateFrom}");

            if (!string.IsNullOrEmpty(DateTo))
                queryParams.Add($"toDate={DateTo}");

            var queryString = queryParams.Any() ? $"?{string.Join("&", queryParams)}" : "";

            return format.ToLower() == "csv"
                ? $"/api/assethistories/export/csv{queryString}"
                : $"/api/assethistories/export/json{queryString}";
        }

        private async Task LoadAvailableFilters()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/assethistories/filters");
                if (response.IsSuccessStatusCode)
                {
                    AvailableFilters = await response.Content.ReadFromJsonAsync<AssetHistoryFilters>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load available filters");
                AvailableFilters = new AssetHistoryFilters();
            }
        }

        // Helper method to get badge color based on action
        public string GetActionBadgeClass(string action)
        {
            return action?.ToUpper() switch
            {
                "CREATE" => "bg-success",
                "UPDATE" => "bg-primary",
                "DELETE" => "bg-danger",
                "TRANSFER" => "bg-info",
                "MAINTENANCE" => "bg-warning",
                "CHECKOUT" => "bg-purple",
                "CHECKIN" => "bg-teal",
                _ => "bg-secondary"
            };
        }

        // Helper method to get action icon
        public string GetActionIcon(string action)
        {
            return action?.ToUpper() switch
            {
                "CREATE" => "bi-plus-circle",
                "UPDATE" => "bi-pencil",
                "DELETE" => "bi-trash",
                "TRANSFER" => "bi-arrow-left-right",
                "MAINTENANCE" => "bi-tools",
                "CHECKOUT" => "bi-box-arrow-right",
                "CHECKIN" => "bi-box-arrow-in-left",
                _ => "bi-activity"
            };
        }

        // Helper method to truncate strings
        public string TruncateString(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, Math.Min(value.Length, maxLength - 3)) + "...";
        }
    }

    // String extension methods - added here in the same file
    public static class StringExtensions
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, Math.Min(value.Length, maxLength - 3)) + "...";
        }
    }
}