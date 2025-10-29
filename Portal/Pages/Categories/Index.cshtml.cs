using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared.DTOs;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Portal.Pages.Categories
{
    public class IndexModel : PageModel
    {
        private readonly HttpClient _httpClient;

        public IndexModel(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("AssetTagApi"); // Assume named client configured with base address and auth
        }

        public List<CategoryReadDTO> Categories { get; set; } = new();

        [BindProperty]
        public CategoryCreateDTO CreateDto { get; set; } = new CategoryCreateDTO();

        [BindProperty]
        public CategoryUpdateDTO UpdateDto { get; set; } = new CategoryUpdateDTO();

        public string? ActiveModal { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Categories = await _httpClient.GetFromJsonAsync<List<CategoryReadDTO>>("api/categories") ?? new List<CategoryReadDTO>();
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            if (!ModelState.IsValid)
            {
                ActiveModal = "create";
                await OnGetAsync();
                return Page();
            }

            var response = await _httpClient.PostAsJsonAsync("api/categories", CreateDto);
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage();
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("CreateDto.Name", "Category name already exists.");
            }
            // Handle other errors if needed

            ActiveModal = "create";
            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync()
        {
            if (string.IsNullOrEmpty(UpdateDto.CategoryId))
            {
                ActiveModal = "edit";
                await OnGetAsync();
                return Page();
            }

            var response = await _httpClient.PutAsJsonAsync($"api/categories/{UpdateDto.CategoryId}", UpdateDto);
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage();
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("UpdateDto.Name", "Category name already exists.");
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Handle not found, perhaps TempData message
            }

            ActiveModal = "edit";
            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToPage();
            }

            var response = await _httpClient.DeleteAsync($"api/categories/{id}");
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage();
            }

            // Handle error, e.g., not found or conflict if dependencies exist (API doesn't check dependencies)
            await OnGetAsync();
            return Page();
        }
    }
}