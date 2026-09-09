using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared.Constants;
using Shared.DTOs;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Portal.Pages.Categories
{
    [Authorize(Roles = RoleNames.Admin)]
    public class IndexModel : PageModel
    {
        private readonly HttpClient _httpClient;

        public IndexModel(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("AssetTagApi");
        }

        public List<CategoryReadDTO> Categories { get; set; } = new();
        public PaginatedResponse<CategoryReadDTO> PagedCategories { get; set; } = new();
        public CategoryCreateDTO CreateDto { get; set; } = new CategoryCreateDTO();
        public CategoryUpdateDTO UpdateDto { get; set; } = new CategoryUpdateDTO();
        public string? ActiveModal { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = AssetConstants.Pagination.DefaultPageSize;

        public async Task<IActionResult> OnGetAsync()
        {
            if (CurrentPage < 1) CurrentPage = 1;
            if (PageSize < 1) PageSize = AssetConstants.Pagination.DefaultPageSize;

            var response = await _httpClient.GetAsync($"api/categories?page={CurrentPage}&pageSize={PageSize}");
            if (response.IsSuccessStatusCode)
            {
                PagedCategories = await response.Content.ReadFromJsonAsync<PaginatedResponse<CategoryReadDTO>>()
                    ?? new PaginatedResponse<CategoryReadDTO>();
                Categories = PagedCategories.Data;

                if (PagedCategories.TotalPages > 0 && CurrentPage > PagedCategories.TotalPages)
                {
                    return RedirectToPage("./Index", new
                    {
                        currentPage = PagedCategories.TotalPages,
                        pageSize = PageSize
                    });
                }
            }

            return Page();
        }

        public string GetPageUrl(int page) =>
            Url.Page("./Index", new { currentPage = page, pageSize = PageSize }) ?? "#";

        public Portal.ViewModels.PaginationViewModel Pagination =>
            Portal.ViewModels.PaginationViewModel.FromPaginated(
                PagedCategories,
                GetPageUrl,
                "Categories pagination",
                cssClass: "mt-3");

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
                return RedirectToPage(new { currentPage = 1, pageSize = PageSize });
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            ModelState.AddModelError("", $"Failed to create category: {response.StatusCode} - {errorContent}");

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("CreateDto.Name", "Category name already exists.");
            }

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
                return RedirectToPage(new { currentPage = CurrentPage, pageSize = PageSize });
            }

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

        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Invalid category ID.";
                return RedirectToPage(new { currentPage = CurrentPage, pageSize = PageSize });
            }

            var response = await _httpClient.DeleteAsync($"api/categories/{id}");

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Category deleted successfully.";
                return RedirectToPage(new { currentPage = CurrentPage, pageSize = PageSize });
            }

            string errorMsg = "Failed to delete category.";

            if (response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.Conflict)
            {
                var errorContent = await response.Content.ReadAsStringAsync();

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

            await OnGetAsync();
            return Page();
        }
    }
}
