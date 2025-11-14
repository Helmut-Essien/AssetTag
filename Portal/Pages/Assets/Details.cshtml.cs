using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Portal.Pages.Assets
{
    public class DetailsModel : PageModel
    {
        private readonly HttpClient _httpClient;

        public DetailsModel(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("AssetTagApi");
        }

        public AssetReadDTO? Asset { get; set; }
        public PaginatedResponse<AssetHistoryReadDTO> AssetHistories { get; set; } = new();
        public List<CategoryReadDTO> Categories { get; set; } = new();
        public List<LocationReadDTO> Locations { get; set; } = new();
        public List<DepartmentReadDTO> Departments { get; set; } = new();

        // Filter properties
        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 10;

        [BindProperty(SupportsGet = true)]
        public string? ActionFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DateFrom { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DateTo { get; set; }

        // Lookup dictionaries
        private Dictionary<string, string> _categoryNames = new();
        private Dictionary<string, string> _locationNames = new();
        private Dictionary<string, string> _departmentNames = new();

        public string CategoryName => Asset != null ? GetCategoryName(Asset.CategoryId) : "Unknown";
        public string LocationName => Asset != null ? GetLocationName(Asset.LocationId) : "Unknown";
        public string DepartmentName => Asset != null ? GetDepartmentName(Asset.DepartmentId) : "Unknown";
        public int AssetAge => Asset?.PurchaseDate != null ? (int)(DateTime.Now - Asset.PurchaseDate.Value).TotalDays : 0;

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            await LoadReferenceData();

            // Load asset details
            var assetResponse = await _httpClient.GetAsync($"api/assets/{id}");
            if (!assetResponse.IsSuccessStatusCode)
            {
                return NotFound();
            }

            Asset = await assetResponse.Content.ReadFromJsonAsync<AssetReadDTO>();

            // Build query string for history with filters
            var queryParams = new List<string>
            {
                $"page={CurrentPage}",
                $"pageSize={PageSize}"
            };

            if (!string.IsNullOrEmpty(ActionFilter))
                queryParams.Add($"action={ActionFilter}");

            if (!string.IsNullOrEmpty(DateFrom) && DateTime.TryParse(DateFrom, out _))
                queryParams.Add($"fromDate={DateFrom}");

            if (!string.IsNullOrEmpty(DateTo) && DateTime.TryParse(DateTo, out _))
                queryParams.Add($"toDate={DateTo}");

            var queryString = string.Join("&", queryParams);

            // Load asset history with pagination and filters
            var historyResponse = await _httpClient.GetAsync($"api/assethistories/asset/{id}?{queryString}");
            if (historyResponse.IsSuccessStatusCode)
            {
                AssetHistories = await historyResponse.Content.ReadFromJsonAsync<PaginatedResponse<AssetHistoryReadDTO>>() ?? new PaginatedResponse<AssetHistoryReadDTO>();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostApplyFiltersAsync(string id)
        {
            // Reset to first page when applying new filters
            CurrentPage = 1;
            return await OnGetAsync(id);
        }

        public async Task<IActionResult> OnPostClearFiltersAsync(string id)
        {
            CurrentPage = 1;
            ActionFilter = null;
            DateFrom = null;
            DateTo = null;
            return await OnGetAsync(id);
        }

        private async Task LoadReferenceData()
        {
            var categoriesTask = _httpClient.GetFromJsonAsync<List<CategoryReadDTO>>("api/categories");
            var locationsTask = _httpClient.GetFromJsonAsync<List<LocationReadDTO>>("api/locations");
            var departmentsTask = _httpClient.GetFromJsonAsync<List<DepartmentReadDTO>>("api/departments");

            await Task.WhenAll(categoriesTask, locationsTask, departmentsTask);

            Categories = categoriesTask.Result ?? new List<CategoryReadDTO>();
            Locations = locationsTask.Result ?? new List<LocationReadDTO>();
            Departments = departmentsTask.Result ?? new List<DepartmentReadDTO>();

            _categoryNames = Categories.ToDictionary(c => c.CategoryId, c => c.Name);
            _locationNames = Locations.ToDictionary(l => l.LocationId, l => $"{l.Name} - {l.Campus}");
            _departmentNames = Departments.ToDictionary(d => d.DepartmentId, d => d.Name);
        }

        public string GetCategoryName(string categoryId)
        {
            return _categoryNames.GetValueOrDefault(categoryId, "Unknown Category");
        }

        public string GetLocationName(string locationId)
        {
            return _locationNames.GetValueOrDefault(locationId, "Unknown Location");
        }

        public string GetDepartmentName(string departmentId)
        {
            return _departmentNames.GetValueOrDefault(departmentId, "Unknown Department");
        }

        // Helper method to generate page links
        public string GetPageUrl(int page)
        {
            var queryParams = new List<string>
            {
                $"id={Asset?.AssetId}",
                $"CurrentPage={page}",
                $"PageSize={PageSize}"
            };

            if (!string.IsNullOrEmpty(ActionFilter))
                queryParams.Add($"ActionFilter={ActionFilter}");

            if (!string.IsNullOrEmpty(DateFrom))
                queryParams.Add($"DateFrom={DateFrom}");

            if (!string.IsNullOrEmpty(DateTo))
                queryParams.Add($"DateTo={DateTo}");

            return $"./Details?{string.Join("&", queryParams)}";
        }
    }

    // Add the PaginatedResponse class if not already in Shared.DTOs
    
}