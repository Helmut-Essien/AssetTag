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
        public List<UserReadDTO> Users { get; set; } = new();

        [BindProperty]
        public AssetCreateDTO CreateDto { get; set; } = new AssetCreateDTO();

        [BindProperty]
        public AssetUpdateDTO UpdateDto { get; set; } = new AssetUpdateDTO();

        public string? ActiveModal { get; set; }
        public string? ToastMessage { get; set; }
        public string? ToastType { get; set; }

        public async Task<IActionResult> OnGetAsync(string? toastMessage = null, string? toastType = null)
        {
            if (!string.IsNullOrEmpty(toastMessage))
            {
                ToastMessage = toastMessage;
                ToastType = toastType ?? "success";
            }

            await LoadDataAsync();
            return Page();
        }

        private async Task LoadDataAsync()
        {
            // Load assets
            Assets = await _httpClient.GetFromJsonAsync<List<AssetReadDTO>>("api/assets") ?? new List<AssetReadDTO>();

            // Load dropdown data
            Categories = await _httpClient.GetFromJsonAsync<List<CategoryReadDTO>>("api/categories") ?? new List<CategoryReadDTO>();
            Locations = await _httpClient.GetFromJsonAsync<List<LocationReadDTO>>("api/locations") ?? new List<LocationReadDTO>();
            Departments = await _httpClient.GetFromJsonAsync<List<DepartmentReadDTO>>("api/departments") ?? new List<DepartmentReadDTO>();
            Users = await _httpClient.GetFromJsonAsync<List<UserReadDTO>>("api/auth") ?? new List<UserReadDTO>();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            if (!ModelState.IsValid)
            {
                ActiveModal = "create";
                await LoadDataAsync();
                return Page();
            }

            var response = await _httpClient.PostAsJsonAsync("api/assets", CreateDto);
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage(new { toastMessage = "Asset created successfully", toastType = "success" });
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("CreateDto.AssetTag", "Asset tag already exists.");
            }
            else
            {
                ModelState.AddModelError("", $"Failed to create asset: {errorContent}");
            }

            ActiveModal = "create";
            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync()
        {
            if (!ModelState.IsValid)
            {
                ActiveModal = "edit";
                await LoadDataAsync();
                return Page();
            }

            var response = await _httpClient.PutAsJsonAsync($"api/assets/{UpdateDto.AssetId}", UpdateDto);
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage(new { toastMessage = "Asset updated successfully", toastType = "success" });
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("UpdateDto.AssetTag", "Asset tag already exists.");
            }
            else
            {
                ModelState.AddModelError("", $"Failed to update asset: {errorContent}");
            }

            ActiveModal = "edit";
            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToPage(new { toastMessage = "Invalid asset ID", toastType = "error" });
            }

            var response = await _httpClient.DeleteAsync($"api/assets/{id}");
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage(new { toastMessage = "Asset deleted successfully", toastType = "success" });
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return RedirectToPage(new { toastMessage = $"Failed to delete asset: {errorContent}", toastType = "error" });
        }
    }
}