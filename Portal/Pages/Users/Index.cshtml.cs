using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Shared.DTOs;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using System.Web;

namespace Portal.Pages.Users
{
    public class IndexModel : PageModel
    {
        private readonly HttpClient _httpClient;

        public IndexModel(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("AssetTagApi");
        }

        public List<UserReadDTO> Users { get; set; } = new();
        public UserUpdateDTO UpdateDto { get; set; } = new UserUpdateDTO();
        public AssignRoleDTO AssignRoleDto { get; set; } = new AssignRoleDTO(string.Empty, string.Empty);
        public string? ActiveModal { get; set; }
        public string? SelectedUserId { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<SelectListItem> Departments { get; set; } = new();
        public string? ResetToken { get; set; }
        public string? Message { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DepartmentId { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? IsActive { get; set; }

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }

        public async Task<IActionResult> OnGetAsync(int page = 1)
        {
            CurrentPage = page;

            var queryString = $"?page={CurrentPage}&pageSize={PageSize}";
            if (!string.IsNullOrEmpty(Search)) queryString += $"&search={HttpUtility.UrlEncode(Search)}";
            if (!string.IsNullOrEmpty(DepartmentId)) queryString += $"&departmentId={DepartmentId}";
            if (IsActive.HasValue) queryString += $"&isActive={IsActive.Value}";

            var response = await _httpClient.GetAsync($"api/users{queryString}");
            if (response.IsSuccessStatusCode)
            {
                Users = await response.Content.ReadFromJsonAsync<List<UserReadDTO>>() ?? new List<UserReadDTO>();

                if (response.Headers.TryGetValues("X-Total-Count", out var totalValues))
                {
                    TotalCount = int.Parse(totalValues.FirstOrDefault() ?? "0");
                }
            }
            else
            {
                Message = $"Failed to load users: {response.StatusCode}";
            }

            // Load departments for dropdown
            await LoadDepartments();

            return Page();
        }

        private async Task LoadDepartments()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/departments");
                if (response.IsSuccessStatusCode)
                {
                    var departments = await response.Content.ReadFromJsonAsync<List<DepartmentReadDTO>>() ?? new List<DepartmentReadDTO>();
                    Departments = departments.Select(d => new SelectListItem
                    {
                        Value = d.DepartmentId,
                        Text = d.Name
                    }).ToList();

                    // Add empty option
                    Departments.Insert(0, new SelectListItem("Select Department", ""));
                }
            }
            catch (Exception ex)
            {
                Message = $"Failed to load departments: {ex.Message}";
            }
        }

        public async Task<IActionResult> OnPostUpdateAsync([Bind(Prefix = "UpdateDto")] UserUpdateDTO dto)
        {
            if (string.IsNullOrEmpty(dto.Id))
            {
                ActiveModal = "edit";
                UpdateDto = dto;
                await OnGetAsync(CurrentPage);
                return Page();
            }

            if (!ModelState.IsValid)
            {
                ActiveModal = "edit";
                UpdateDto = dto;
                await OnGetAsync(CurrentPage);
                return Page();
            }

            var response = await _httpClient.PutAsJsonAsync($"api/users/{dto.Id}", dto);
            if (response.IsSuccessStatusCode)
            {
                // FIX: Use proper redirect
                return RedirectToPage("./Index", new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            ModelState.AddModelError(string.Empty, $"Failed to update user: {response.StatusCode} - {errorContent}");

            ActiveModal = "edit";
            UpdateDto = dto;
            await OnGetAsync(CurrentPage);
            return Page();
        }

        public async Task<IActionResult> OnPostToggleActivationAsync(string id, bool isActive)
        {
            if (string.IsNullOrEmpty(id))
            {
                // FIX: Use proper redirect
                return RedirectToPage("./Index", new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });
            }

            var response = await _httpClient.PatchAsJsonAsync($"api/users/{id}/activation", isActive);
            if (response.IsSuccessStatusCode)
            {
                // FIX: Use proper redirect
                return RedirectToPage("./Index", new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            ModelState.AddModelError(string.Empty, $"Failed to toggle activation: {response.StatusCode} - {errorContent}");

            await OnGetAsync(CurrentPage);
            return Page();
        }

        //public async Task<IActionResult> OnPostDeactivateAsync(string id)
        //{
        //    if (string.IsNullOrEmpty(id))
        //    {
        //        return RedirectToPage(new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });
        //    }

        //    var response = await _httpClient.DeleteAsync($"api/users/{id}");
        //    if (response.IsSuccessStatusCode)
        //    {
        //        return RedirectToPage(new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });
        //    }

        //    var errorContent = await response.Content.ReadAsStringAsync();
        //    ModelState.AddModelError(string.Empty, $"Failed to deactivate user: {response.StatusCode} - {errorContent}");

        //    await OnGetAsync(CurrentPage);
        //    return Page();
        //}









        //public async Task<IActionResult> OnGetRolesAsync(string id)
        //{
        //    SelectedUserId = id;
        //    var response = await _httpClient.GetAsync($"api/users/{id}/roles");
        //    if (response.IsSuccessStatusCode)
        //    {
        //        Roles = await response.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
        //    }
        //    else
        //    {
        //        var errorContent = await response.Content.ReadAsStringAsync();
        //        Message = $"Failed to load roles: {response.StatusCode} - {errorContent}";
        //    }

        //    ActiveModal = "roles";
        //    await OnGetAsync(CurrentPage);
        //    return Page();
        //}

        //public async Task<IActionResult> OnPostAddRoleAsync(string id, [Bind(Prefix = "AssignRoleDto.RoleName")] string roleName)
        //{
        //    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(roleName))
        //    {
        //        ActiveModal = "roles";
        //        SelectedUserId = id;
        //        await OnGetRolesAsync(id);
        //        return Page();
        //    }

        //    var dto = new AssignRoleDTO("", roleName); // Email not used in this context
        //    var response = await _httpClient.PostAsJsonAsync($"api/users/{id}/roles", dto);
        //    if (response.IsSuccessStatusCode)
        //    {
        //        // FIX: Use proper redirect
        //        return RedirectToPage("./Index", new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });
        //    }

        //    var errorContent = await response.Content.ReadAsStringAsync();
        //    ModelState.AddModelError("AssignRoleDto.RoleName", $"Failed to add role: {response.StatusCode} - {errorContent}");

        //    ActiveModal = "roles";
        //    SelectedUserId = id;
        //    AssignRoleDto = new AssignRoleDTO("", roleName);
        //    await OnGetRolesAsync(id);
        //    return Page();
        //}

        //public async Task<IActionResult> OnPostRemoveRoleAsync(string id, string roleName)
        //{
        //    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(roleName))
        //    {
        //        ActiveModal = "roles";
        //        SelectedUserId = id;
        //        await OnGetRolesAsync(id);
        //        return Page();
        //    }

        //    var dto = new AssignRoleDTO("", roleName); // Email not used
        //    var request = new HttpRequestMessage
        //    {
        //        Method = HttpMethod.Delete,
        //        RequestUri = new Uri($"{_httpClient.BaseAddress}api/users/{id}/roles"),
        //        Content = JsonContent.Create(dto)
        //    };

        //    var response = await _httpClient.SendAsync(request);
        //    if (response.IsSuccessStatusCode)
        //    {
        //        // FIX: Use proper redirect
        //        return RedirectToPage("./Index", new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });
        //    }

        //    var errorContent = await response.Content.ReadAsStringAsync();
        //    ModelState.AddModelError(string.Empty, $"Failed to remove role: {response.StatusCode} - {errorContent}");

        //    ActiveModal = "roles";
        //    SelectedUserId = id;
        //    await OnGetRolesAsync(id);
        //    return Page();
        //}


        // Add these methods to your existing IndexModel class

        public async Task<JsonResult> OnGetRolesDataAsync(string id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/users/{id}/roles");
                if (response.IsSuccessStatusCode)
                {
                    var roles = await response.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
                    return new JsonResult(new { userId = id, roles = roles });
                }
                else
                {
                    return new JsonResult(new { userId = id, roles = new List<string>() });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { userId = id, roles = new List<string>(), error = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostAddRoleAsync(string id, string roleName)
        {
            try
            {
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(roleName))
                {
                    return BadRequest("User ID and role name are required.");
                }

                var dto = new AssignRoleDTO("", roleName);
                var response = await _httpClient.PostAsJsonAsync($"api/users/{id}/roles", dto);

                if (response.IsSuccessStatusCode)
                {
                    return Content("Role added successfully.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return BadRequest($"Failed to add role: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error adding role: {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostRemoveRoleAsync(string id, string roleName)
        {
            try
            {
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(roleName))
                {
                    return BadRequest("User ID and role name are required.");
                }

                var dto = new AssignRoleDTO("", roleName);
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri($"{_httpClient.BaseAddress}api/users/{id}/roles"),
                    Content = JsonContent.Create(dto)
                };

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return Content("Role removed successfully.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return BadRequest($"Failed to remove role: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error removing role: {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                // FIX: Use proper redirect
                return RedirectToPage("./Index", new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });
            }

            var response = await _httpClient.PostAsync($"api/users/{id}/password-reset", null);
            if (response.IsSuccessStatusCode)
            {
                ResetToken = await response.Content.ReadAsStringAsync();
                Message = $"Password reset token generated: {ResetToken}. Please send this to the user securely.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Message = $"Failed to reset password: {response.StatusCode} - {errorContent}";
            }

            await OnGetAsync(CurrentPage);
            return Page();
        }
    }
}