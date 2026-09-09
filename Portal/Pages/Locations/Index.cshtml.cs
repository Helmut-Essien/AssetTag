using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared.Constants;
using Shared.DTOs;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Portal.Pages.Locations
{
    [Authorize(Roles = RoleNames.Admin)]
    public class IndexModel : PageModel
    {
        private readonly HttpClient _httpClient;

        public IndexModel(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("AssetTagApi");
        }

        public List<LocationReadDTO> Locations { get; set; } = new();
        public PaginatedResponse<LocationReadDTO> PagedLocations { get; set; } = new();
        public LocationCreateDTO CreateDto { get; set; } = new LocationCreateDTO();
        public LocationUpdateDTO UpdateDto { get; set; } = new LocationUpdateDTO();
        public string? ActiveModal { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = AssetConstants.Pagination.DefaultPageSize;

        public async Task<IActionResult> OnGetAsync()
        {
            if (CurrentPage < 1) CurrentPage = 1;
            if (PageSize < 1) PageSize = AssetConstants.Pagination.DefaultPageSize;

            var response = await _httpClient.GetAsync($"api/locations?page={CurrentPage}&pageSize={PageSize}");
            if (response.IsSuccessStatusCode)
            {
                PagedLocations = await response.Content.ReadFromJsonAsync<PaginatedResponse<LocationReadDTO>>()
                    ?? new PaginatedResponse<LocationReadDTO>();
                Locations = PagedLocations.Data;

                if (PagedLocations.TotalPages > 0 && CurrentPage > PagedLocations.TotalPages)
                {
                    return RedirectToPage("./Index", new
                    {
                        currentPage = PagedLocations.TotalPages,
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
                PagedLocations,
                GetPageUrl,
                "Locations pagination",
                cssClass: "mt-3");

        public async Task<IActionResult> OnPostCreateAsync([Bind(Prefix = "CreateDto")] LocationCreateDTO dto)
        {
            if (!ModelState.IsValid)
            {
                ActiveModal = "create";
                CreateDto = dto;
                await OnGetAsync();
                return Page();
            }

            var response = await _httpClient.PostAsJsonAsync("api/locations", dto);
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage(new { currentPage = 1, pageSize = PageSize });
            }

            var errorContent = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("CreateDto.Name", "A location with the same name and campus already exists.");
            }
            else
            {
                ModelState.AddModelError("", $"Failed to create location: {response.StatusCode} - {errorContent}");
            }

            ActiveModal = "create";
            CreateDto = dto;
            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync([Bind(Prefix = "UpdateDto")] LocationUpdateDTO dto)
        {
            if (string.IsNullOrEmpty(dto.LocationId))
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

            var response = await _httpClient.PutAsJsonAsync($"api/locations/{dto.LocationId}", dto);
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage(new { currentPage = CurrentPage, pageSize = PageSize });
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("UpdateDto.Name", "A location with the same name and campus already exists.");
            }
            else
            {
                var text = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, text ?? "Failed to update location");
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
                TempData["ErrorMessage"] = "Invalid location ID.";
                return RedirectToPage(new { currentPage = CurrentPage, pageSize = PageSize });
            }

            var response = await _httpClient.DeleteAsync($"api/locations/{id}");

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Location deleted successfully.";
                return RedirectToPage(new { currentPage = CurrentPage, pageSize = PageSize });
            }

            // Handle specific errors
            string errorMsg = "Failed to delete location.";

            if (response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.Conflict)
            {
                var errorContent = await response.Content.ReadAsStringAsync();

                if (errorContent.Contains("REFERENCE constraint") ||
                    errorContent.Contains("FK_Assets_Locations") ||
                    errorContent.Contains("FK_AssetHistories_Locations") ||
                    errorContent.Contains("in use") ||
                    errorContent.Contains("assigned to"))
                {
                    errorMsg = "Cannot delete this location because it is still assigned to one or more assets. Reassign or remove the assets first.";
                }
                else
                {
                    errorMsg += $" ({response.StatusCode}) - {errorContent}";
                }
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                errorMsg = "Location not found.";
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
