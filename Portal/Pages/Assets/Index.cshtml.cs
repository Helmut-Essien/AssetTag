using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared.DTOs;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Portal.Pages.Assets
{
    public class IndexModel : PageModel
    {
        private readonly HttpClient _httpClient;

        public IndexModel(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("AssetTagApi");
        }

        public List<AssetReadDTO> Assets { get; set; } = new();
        public List<CategoryReadDTO> Categories { get; set; } = new();
        public List<LocationReadDTO> Locations { get; set; } = new();
        public List<DepartmentReadDTO> Departments { get; set; } = new();

        // Filter properties
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? ConditionFilter { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? CategoryFilter { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? LocationFilter { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? DepartmentFilter { get; set; } = string.Empty;

        public AssetCreateDTO CreateDto { get; set; } = new AssetCreateDTO();
        public AssetUpdateDTO UpdateDto { get; set; } = new AssetUpdateDTO();
        public string? ActiveModal { get; set; }

        // Dictionaries for name lookups
        private Dictionary<string, string> _categoryNames = new();
        private Dictionary<string, string> _locationNames = new();
        private Dictionary<string, string> _departmentNames = new();

        // Performance cache
        private static List<AssetReadDTO>? _cachedAssets;
        private static System.DateTime _lastCacheUpdate = System.DateTime.MinValue;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                await LoadDataAsync();
                // Apply filters client-side for better performance
                if (!string.IsNullOrEmpty(SearchTerm) ||
                    !string.IsNullOrEmpty(StatusFilter) ||
                    !string.IsNullOrEmpty(ConditionFilter) ||
                    !string.IsNullOrEmpty(CategoryFilter) ||
                    !string.IsNullOrEmpty(LocationFilter) ||
                    !string.IsNullOrEmpty(DepartmentFilter))
                {
                    Assets = ApplyFilters(Assets);
                }
                return Page();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized || ex.StatusCode == HttpStatusCode.Forbidden)
            {
                // The handler should have redirected, but if we get here, redirect manually
                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return RedirectToPage("/Unauthorized");
                }
                else
                {
                    return RedirectToPage("/Forbidden");
                }
            }
        }

        private async Task LoadDataAsync()
        {
            // Cache assets for 30 seconds to reduce API calls
            if (_cachedAssets == null || (System.DateTime.Now - _lastCacheUpdate).TotalSeconds > 30)
            {
                var assetsResponse = await _httpClient.GetAsync("api/assets");
                if (assetsResponse.IsSuccessStatusCode)
                {
                    _cachedAssets = await assetsResponse.Content.ReadFromJsonAsync<List<AssetReadDTO>>() ?? new List<AssetReadDTO>();
                    _lastCacheUpdate = System.DateTime.Now;
                }
            }

            Assets = _cachedAssets ?? new List<AssetReadDTO>();
            // Load reference data in parallel for better performance
            var categoriesTask = _httpClient.GetFromJsonAsync<List<CategoryReadDTO>>("api/categories");
            var locationsTask = _httpClient.GetFromJsonAsync<List<LocationReadDTO>>("api/locations");
            var departmentsTask = _httpClient.GetFromJsonAsync<List<DepartmentReadDTO>>("api/departments");

            await Task.WhenAll(categoriesTask, locationsTask, departmentsTask);

            Categories = categoriesTask.Result ?? new List<CategoryReadDTO>();
            Locations = locationsTask.Result ?? new List<LocationReadDTO>();
            Departments = departmentsTask.Result ?? new List<DepartmentReadDTO>();

            // Build lookup dictionaries
            _categoryNames = Categories.ToDictionary(c => c.CategoryId, c => c.Name);
            _locationNames = Locations.ToDictionary(l => l.LocationId, l => $"{l.Name} - {l.Campus}");
            _departmentNames = Departments.ToDictionary(d => d.DepartmentId, d => d.Name);
        }

        private List<AssetReadDTO> ApplyFilters(List<AssetReadDTO> assets)
        {
            var filtered = assets.AsEnumerable();

            if (!string.IsNullOrEmpty(SearchTerm))
            {
                var search = SearchTerm.ToLowerInvariant();
                filtered = filtered.Where(a =>
                    (a.AssetTag?.ToLowerInvariant().Contains(search) ?? false) ||
                    (a.Name?.ToLowerInvariant().Contains(search) ?? false) ||
                    (a.Description?.ToLowerInvariant().Contains(search) ?? false) ||
                    (a.SerialNumber?.ToLowerInvariant().Contains(search) ?? false) ||
                    (a.VendorName?.ToLowerInvariant().Contains(search) ?? false) ||
                    (a.InvoiceNumber?.ToLowerInvariant().Contains(search) ?? false)
                );
            }

            if (!string.IsNullOrEmpty(StatusFilter))
                filtered = filtered.Where(a => a.Status == StatusFilter);

            if (!string.IsNullOrEmpty(ConditionFilter))
                filtered = filtered.Where(a => a.Condition == ConditionFilter);

            if (!string.IsNullOrEmpty(CategoryFilter))
                filtered = filtered.Where(a => a.CategoryId == CategoryFilter);

            if (!string.IsNullOrEmpty(LocationFilter))
                filtered = filtered.Where(a => a.LocationId == LocationFilter);

            if (!string.IsNullOrEmpty(DepartmentFilter))
                filtered = filtered.Where(a => a.DepartmentId == DepartmentFilter);

            return filtered.ToList();
        }

        // Helper methods to get names from IDs
        // Helper methods for name lookups
        public string GetCategoryName(string categoryId) =>
            _categoryNames.GetValueOrDefault(categoryId, "Unknown Category");

        public string GetLocationName(string locationId) =>
            _locationNames.GetValueOrDefault(locationId, "Unknown Location");

        public string GetDepartmentName(string departmentId) =>
            _departmentNames.GetValueOrDefault(departmentId, "Unknown Department");

        public async Task<IActionResult> OnPostCreateAsync([Bind(Prefix = "CreateDto")] AssetCreateDTO dto)
        {
            if (!ModelState.IsValid)
            {
                ActiveModal = "create";
                CreateDto = dto;
                await LoadDataAsync();
                return Page();
            }

            var response = await _httpClient.PostAsJsonAsync("api/assets", dto);
            if (response.IsSuccessStatusCode)
            {
                // Invalidate cache on create
                _cachedAssets = null;
                return RedirectToPage();
            }

            var errorContent = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("CreateDto.AssetTag", "An asset with this tag already exists.");
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return RedirectToPage("/Unauthorized");
            }
            else if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return RedirectToPage("/Forbidden");
            }
            else
            {
                ModelState.AddModelError("", $"Failed to create asset: {response.StatusCode} - {errorContent}");
            }

            ActiveModal = "create";
            CreateDto = dto;
            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync([Bind(Prefix = "UpdateDto")] AssetUpdateDTO dto)
        {
            if (string.IsNullOrEmpty(dto.AssetId))
            {
                ActiveModal = "edit";
                UpdateDto = dto;
                await LoadDataAsync();
                return Page();
            }

            if (!ModelState.IsValid)
            {
                ActiveModal = "edit";
                UpdateDto = dto;
                await LoadDataAsync();
                return Page();
            }

            var response = await _httpClient.PutAsJsonAsync($"api/assets/{dto.AssetId}", dto);
            if (response.IsSuccessStatusCode)
            {
                // Invalidate cache on update
                _cachedAssets = null;
                return RedirectToPage();
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("UpdateDto.AssetTag", "An asset with this tag already exists.");
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return RedirectToPage("/Unauthorized");
            }
            else if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return RedirectToPage("/Forbidden");
            }
            else
            {
                var text = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, text ?? "Failed to update asset");
            }

            ActiveModal = "edit";
            UpdateDto = dto;
            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToPage();
            }

            var response = await _httpClient.DeleteAsync($"api/assets/{id}");
            if (response.IsSuccessStatusCode)
            {
                // Invalidate cache on delete
                _cachedAssets = null;
                return RedirectToPage();
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return RedirectToPage("/Unauthorized");
            }
            else if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return RedirectToPage("/Forbidden");
            }

            await LoadDataAsync();
            return Page();
        }

        // Quick filter actions
        public IActionResult OnGetClearFilters()
        {
            SearchTerm = string.Empty;
            StatusFilter = string.Empty;
            ConditionFilter = string.Empty;
            CategoryFilter = string.Empty;
            LocationFilter = string.Empty;
            DepartmentFilter = string.Empty;

            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetExportFilteredAsync()
        {
            await LoadDataAsync();
            var filteredAssets = ApplyFilters(Assets);

            // In a real implementation, you'd generate CSV/Excel here
            // For now, just return to page with filtered data
            Assets = filteredAssets;
            return Page();
        }
    }
}