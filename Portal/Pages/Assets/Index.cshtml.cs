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

        public AssetCreateDTO CreateDto { get; set; } = new AssetCreateDTO();
        public AssetUpdateDTO UpdateDto { get; set; } = new AssetUpdateDTO();
        public string? ActiveModal { get; set; }

        // Dictionaries for name lookups
        private Dictionary<string, string> _categoryNames = new();
        private Dictionary<string, string> _locationNames = new();
        private Dictionary<string, string> _departmentNames = new();

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadDataAsync();
            return Page();
        }

        private async Task LoadDataAsync()
        {
            // Load all data in parallel
            var assetsTask = _httpClient.GetFromJsonAsync<List<AssetReadDTO>>("api/assets");
            var categoriesTask = _httpClient.GetFromJsonAsync<List<CategoryReadDTO>>("api/categories");
            var locationsTask = _httpClient.GetFromJsonAsync<List<LocationReadDTO>>("api/locations");
            var departmentsTask = _httpClient.GetFromJsonAsync<List<DepartmentReadDTO>>("api/departments");

            await Task.WhenAll(assetsTask, categoriesTask, locationsTask, departmentsTask);

            Assets = assetsTask.Result ?? new List<AssetReadDTO>();
            Categories = categoriesTask.Result ?? new List<CategoryReadDTO>();
            Locations = locationsTask.Result ?? new List<LocationReadDTO>();
            Departments = departmentsTask.Result ?? new List<DepartmentReadDTO>();

            // Build lookup dictionaries
            _categoryNames = Categories.ToDictionary(c => c.CategoryId, c => c.Name);
            _locationNames = Locations.ToDictionary(l => l.LocationId, l => $"{l.Name} - {l.Campus}");
            _departmentNames = Departments.ToDictionary(d => d.DepartmentId, d => d.Name);
        }

        // Helper methods to get names from IDs
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
                return RedirectToPage();
            }

            var errorContent = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("CreateDto.AssetTag", "An asset with this tag already exists.");
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
                return RedirectToPage();
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("UpdateDto.AssetTag", "An asset with this tag already exists.");
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
                return RedirectToPage();
            }

            await LoadDataAsync();
            return Page();
        }
    }
}