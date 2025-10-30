using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared.DTOs;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Portal.Pages.Departments
{
    public class IndexModel : PageModel
    {
        private readonly HttpClient _httpClient;

        public IndexModel(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("AssetTagApi");
        }

        public List<DepartmentReadDTO> Departments { get; set; } = new();
        public DepartmentCreateDTO CreateDto { get; set; } = new DepartmentCreateDTO();
        public DepartmentUpdateDTO UpdateDto { get; set; } = new DepartmentUpdateDTO();
        public string? ActiveModal { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Departments = await _httpClient.GetFromJsonAsync<List<DepartmentReadDTO>>("api/departments") ?? new List<DepartmentReadDTO>();
            return Page();
        }

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
                return RedirectToPage();
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
                return RedirectToPage();
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
                return RedirectToPage();
            }

            var response = await _httpClient.DeleteAsync($"api/departments/{id}");
            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage();
            }

            await OnGetAsync();
            return Page();
        }
    }
}