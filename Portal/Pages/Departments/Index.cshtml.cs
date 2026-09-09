using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared.Constants;
using Shared.DTOs;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Portal.Pages.Departments
{
    [Authorize(Roles = RoleNames.Admin)]
    public class IndexModel : PageModel
    {
        private readonly HttpClient _httpClient;

        public IndexModel(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("AssetTagApi");
        }

        public List<DepartmentReadDTO> Departments { get; set; } = new();
        public PaginatedResponse<DepartmentReadDTO> PagedDepartments { get; set; } = new();
        public DepartmentCreateDTO CreateDto { get; set; } = new DepartmentCreateDTO();
        public DepartmentUpdateDTO UpdateDto { get; set; } = new DepartmentUpdateDTO();
        public string? ActiveModal { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = AssetConstants.Pagination.DefaultPageSize;

        public async Task<IActionResult> OnGetAsync()
        {
            if (CurrentPage < 1) CurrentPage = 1;
            if (PageSize < 1) PageSize = AssetConstants.Pagination.DefaultPageSize;

            var response = await _httpClient.GetAsync($"api/departments?page={CurrentPage}&pageSize={PageSize}");
            if (response.IsSuccessStatusCode)
            {
                PagedDepartments = await response.Content.ReadFromJsonAsync<PaginatedResponse<DepartmentReadDTO>>()
                    ?? new PaginatedResponse<DepartmentReadDTO>();
                Departments = PagedDepartments.Data;

                if (PagedDepartments.TotalPages > 0 && CurrentPage > PagedDepartments.TotalPages)
                {
                    return RedirectToPage("./Index", new
                    {
                        currentPage = PagedDepartments.TotalPages,
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
                PagedDepartments,
                GetPageUrl,
                "Departments pagination",
                cssClass: "mt-3");

        public async Task<IActionResult> OnPostCreateAsync([Bind(Prefix = "CreateDto")] DepartmentCreateDTO dto)
        {
            if (!ModelState.IsValid)
            {
                ActiveModal = "create";
                CreateDto = dto;
                await OnGetAsync();
                return Page();
            }

            var response = await _httpClient.PostAsJsonAsync("api/departments", dto);
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage(new { currentPage = 1, pageSize = PageSize });
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            ModelState.AddModelError("", $"Failed to create department: {response.StatusCode} - {errorContent}");

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("CreateDto.Name", "Department name already exists.");
            }

            ActiveModal = "create";
            CreateDto = dto;
            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync([Bind(Prefix = "UpdateDto")] DepartmentUpdateDTO dto)
        {
            if (string.IsNullOrEmpty(dto.DepartmentId))
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

            var response = await _httpClient.PutAsJsonAsync($"api/departments/{dto.DepartmentId}", dto);
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage(new { currentPage = CurrentPage, pageSize = PageSize });
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
                ModelState.AddModelError("UpdateDto.Name", "Department name already exists.");
            else
            {
                var text = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, text ?? "Failed to update department");
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
                TempData["ErrorMessage"] = "Invalid department ID.";
                return RedirectToPage(new { currentPage = CurrentPage, pageSize = PageSize });
            }

            var response = await _httpClient.DeleteAsync($"api/departments/{id}");

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Department deleted successfully.";
                return RedirectToPage(new { currentPage = CurrentPage, pageSize = PageSize });
            }

            string errorMsg = "Failed to delete department.";

            if (response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.Conflict)
            {
                var errorContent = await response.Content.ReadAsStringAsync();

                if (errorContent.Contains("REFERENCE constraint") ||
                    errorContent.Contains("FK_Assets_Departments") ||
                    errorContent.Contains("FK_AspNetUsers_Departments") ||
                    errorContent.Contains("in use") ||
                    errorContent.Contains("assigned to"))
                {
                    errorMsg = "Cannot delete this department because it is still assigned to one or more assets or users. Reassign or remove them first.";
                }
                else
                {
                    errorMsg += $" ({response.StatusCode}) - {errorContent}";
                }
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                errorMsg = "Department not found.";
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
