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

        public CategoryCreateDTO CreateDto { get; set; } = new CategoryCreateDTO();

        public CategoryUpdateDTO UpdateDto { get; set; } = new CategoryUpdateDTO();

        public string? ActiveModal { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Categories = await _httpClient.GetFromJsonAsync<List<CategoryReadDTO>>("api/categories") ?? new List<CategoryReadDTO>();
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync([Bind(Prefix = "CreateDto")] CategoryCreateDTO dto)
        {
            if (!ModelState.IsValid)
            {
                ActiveModal = "create";
                CreateDto = dto;
                await OnGetAsync();
                return Page();
            }

            var response = await _httpClient.PostAsJsonAsync("api/categories", dto);
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage();
            }

            // Always read error content for logging/debugging
            var errorContent = await response.Content.ReadAsStringAsync();
            ModelState.AddModelError("", $"Failed to create category: {response.StatusCode} - {errorContent}");


            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("CreateDto.Name", "Category name already exists.");
            }
            // Handle other errors if needed

            ActiveModal = "create";
            CreateDto = dto;
            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync([Bind(Prefix = "UpdateDto")] CategoryUpdateDTO dto)
        {
            if (string.IsNullOrEmpty(dto.CategoryId))
            {
                ActiveModal = "edit";
                UpdateDto = dto;
                await OnGetAsync();
                return Page();
            }

            if (!ModelState.IsValid)
            {
                ActiveModal = "edit";
                UpdateDto = dto;
                await OnGetAsync();
                return Page();
            }

            var response = await _httpClient.PutAsJsonAsync($"api/categories/{dto.CategoryId}", dto);
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage();
            }

            // map any API errors into ModelState (prefix with UpdateDto.)
            if (response.StatusCode == HttpStatusCode.Conflict)
                ModelState.AddModelError("UpdateDto.Name", "Category name already exists.");
            else
            {
                var text = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, text ?? "Failed to update category");
            }

            ActiveModal = "edit";
            UpdateDto = dto;
            await OnGetAsync();
            return Page();
        }

        //public async Task<IActionResult> OnPostDeleteAsync(string id)
        //{
        //    if (string.IsNullOrEmpty(id))
        //    {
        //        return RedirectToPage();
        //    }

        //    var response = await _httpClient.DeleteAsync($"api/categories/{id}");
        //    if (response.IsSuccessStatusCode)
        //    {
        //        return RedirectToPage();
        //    }

        //    // Handle error, e.g., not found or conflict if dependencies exist (API doesn't check dependencies)
        //    await OnGetAsync();
        //    return Page();
        //}

        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Invalid category ID.";
                return RedirectToPage();
            }

            var response = await _httpClient.DeleteAsync($"api/categories/{id}");

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Category deleted successfully.";  // optional
                return RedirectToPage();
            }

            // Handle specific errors
            string errorMsg = "Failed to delete category.";

            if (response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.Conflict)  // or whatever your API returns on constraint violation
            {
                var errorContent = await response.Content.ReadAsStringAsync();

                // Customize based on what your API returns in the body
                if (errorContent.Contains("REFERENCE constraint") ||
                    errorContent.Contains("FK_Assets_Categories") ||
                    errorContent.Contains("in use") ||
                    errorContent.Contains("assigned to"))
                {
                    errorMsg = "Cannot delete this category because it is still assigned to one or more assets. Reassign or remove the assets first.";
                }
                else
                {
                    errorMsg += $" ({response.StatusCode}) - {errorContent}";
                }
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                errorMsg = "Category not found.";
            }
            else
            {
                errorMsg += $" Unexpected error ({response.StatusCode}).";
            }

            TempData["ErrorMessage"] = errorMsg;

            // Reload data and return to page (no ActiveModal needed for delete)
            await OnGetAsync();
            return Page();  // or RedirectToPage() if you prefer full redirect
        }
    }
}