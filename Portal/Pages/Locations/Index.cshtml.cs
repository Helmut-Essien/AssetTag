using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared.DTOs;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Portal.Pages.Locations
{
    public class IndexModel : PageModel
    {
        private readonly HttpClient _httpClient;

        public IndexModel(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("AssetTagApi");
        }

        public List<LocationReadDTO> Locations { get; set; } = new();
        public LocationCreateDTO CreateDto { get; set; } = new LocationCreateDTO();
        public LocationUpdateDTO UpdateDto { get; set; } = new LocationUpdateDTO();
        public string? ActiveModal { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Locations = await _httpClient.GetFromJsonAsync<List<LocationReadDTO>>("api/locations") ?? new List<LocationReadDTO>();
            return Page();
        }

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
                return RedirectToPage();
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
                return RedirectToPage();
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
                return RedirectToPage();
            }

            var response = await _httpClient.DeleteAsync($"api/locations/{id}");
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage();
            }

            await OnGetAsync();
            return Page();
        }
    }
}