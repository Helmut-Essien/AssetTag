using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Shared.DTOs;
using System.Net.Http.Json;
using System.Web;

namespace Portal.Pages.Users
{
    public class IndexModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
        {
            _httpClient = httpClientFactory.CreateClient("AssetTagApi");
            _logger = logger;
        }

        public List<UserReadDTO> Users { get; set; } = new();
        public UserUpdateDTO UpdateDto { get; set; } = new UserUpdateDTO();
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

        public List<SelectListItem> Departments { get; set; } = new();
        public List<string> AvailableRoles { get; set; } = new();

        // NEW: Properties for invitation
        [BindProperty]
        public string? InviteEmails { get; set; }

        [BindProperty]
        public string? InviteRole { get; set; } = "User";

        public List<InvitationResponseDTO> Invitations { get; set; } = new();

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

            await LoadDepartments();
            await LoadInvitations();

            // NEW: Load available roles from API
            var rolesResponse = await _httpClient.GetAsync("api/role");
            if (rolesResponse.IsSuccessStatusCode)
            {
                AvailableRoles = await rolesResponse.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
            }
            else
            {
                _logger.LogWarning("Failed to load roles: {StatusCode}", rolesResponse.StatusCode);
                AvailableRoles = new List<string> { "User", "Admin" }; // Fallback to defaults if API fails
            }
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

                    Departments.Insert(0, new SelectListItem("Select Department", ""));
                }
            }
            catch (Exception ex)
            {
                Message = $"Failed to load departments: {ex.Message}";
            }
        }

        public async Task<IActionResult> OnPostUpdateAsync(UserUpdateDTO updateDto)
        {
            if (!ModelState.IsValid)
            {
                await OnGetAsync(CurrentPage);
                return Page();
            }

            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/users/{updateDto.Id}", updateDto);
                if (response.IsSuccessStatusCode)
                {
                    return RedirectToPage("Index", new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });

                }

                var errorContent = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, $"Failed to update user: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", updateDto.Id);
                ModelState.AddModelError(string.Empty, "An error occurred while updating the user.");
            }

            await OnGetAsync(CurrentPage);
            return Page();
        }

        public async Task<IActionResult> OnPostToggleActivationAsync(string id, bool isActive)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToPage(new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });
            }

            try
            {
                var response = await _httpClient.PatchAsJsonAsync($"api/users/{id}/activation", isActive);
                if (response.IsSuccessStatusCode)
                {
                    return RedirectToPage("Index", new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, $"Failed to toggle activation: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling activation for user {UserId}", id);
                ModelState.AddModelError(string.Empty, "An error occurred while updating user status.");
            }

            await OnGetAsync(CurrentPage);
            return Page();
        }

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
                _logger.LogError(ex, "Error loading roles for user {UserId}", id);
                return new JsonResult(new { userId = id, roles = new List<string>(), error = ex.Message });
            }
        }

        public async Task<JsonResult> OnGetAllRolesDataAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/role");
                if (response.IsSuccessStatusCode)
                {
                    var roles = await response.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
                    return new JsonResult(roles);
                }
                else
                {
                    return new JsonResult(new List<string>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all roles");
                return new JsonResult(new { error = ex.Message });
            }
        }

    
        public async Task<IActionResult> OnPostAddRoleAsync(string id, string roleName)
        {
            _logger.LogInformation("OnPostAddRoleAsync called with id: {Id}, roleName: {RoleName}", id, roleName);
            try
            {
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(roleName))
                {
                    _logger.LogWarning("Missing parameters: id={Id}, roleName={RoleName}", id, roleName);
                    return BadRequest("User ID and role name are required.");
                }

                //var dto = new AssignRoleDTO("", roleName);
                // FIX: Use the id parameter
                var dto = new AssignRoleDTO(id, roleName);
                var response = await _httpClient.PostAsJsonAsync($"api/users/{id}/roles", dto);

                if (response.IsSuccessStatusCode)
                {
                    return Content("Role added successfully.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to add role: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    return BadRequest($"Failed to add role: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding role {RoleName} to user {UserId}", roleName, id);
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
                _logger.LogError(ex, "Error removing role {RoleName} from user {UserId}", roleName, id);
                return BadRequest($"Error removing role: {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostCreateRoleAsync(string roleName)
        {
            try
            {
                if (string.IsNullOrEmpty(roleName))
                {
                    return BadRequest("Role name is required.");
                }

                var dto = new CreateRoleDTO(roleName);
                var response = await _httpClient.PostAsJsonAsync($"api/Role/Create", dto);

                if (response.IsSuccessStatusCode)
                {
                    return Content("Role created successfully.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return BadRequest($"Failed to create role: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role {RoleName}", roleName);
                return BadRequest($"Error creating role: {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostDeleteRoleAsync(string roleName)
        {
            try
            {
                if (string.IsNullOrEmpty(roleName))
                {
                    return BadRequest("Role name is required.");
                }

                var response = await _httpClient.DeleteAsync($"api/Role/{roleName}");

                if (response.IsSuccessStatusCode)
                {
                    return Content("Role deleted successfully.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return BadRequest($"Failed to delete role: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role {RoleName}", roleName);
                return BadRequest($"Error deleting role: {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToPage(new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });
            }

            try
            {
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for user {UserId}", id);
                Message = "An error occurred while resetting the password.";
            }

            await OnGetAsync(CurrentPage);
            return Page();
        }


        // NEW: Handle sending invitations
        public async Task<IActionResult> OnPostSendInvitationsAsync()
        {
            if (string.IsNullOrWhiteSpace(InviteEmails))
            {
                Message = "Please enter at least one email address.";
                await OnGetAsync(CurrentPage);
                return Page();
            }

            try
            {
                // Parse emails
                var emails = InviteEmails.Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(email => email.Trim())
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .ToList();

                if (!emails.Any())
                {
                    Message = "Please enter valid email addresses.";
                    await OnGetAsync(CurrentPage);
                    return Page();
                }

                // Validate email formats
                var invalidEmails = emails.Where(email => !IsValidEmail(email)).ToList();
                if (invalidEmails.Any())
                {
                    Message = $"Invalid email format: {string.Join(", ", invalidEmails)}";
                    await OnGetAsync(CurrentPage);
                    return Page();
                }

                var dto = new CreateMultipleInvitationsDTO
                {
                    Emails = emails,
                    Role = InviteRole ?? "User"
                };

                var response = await _httpClient.PostAsJsonAsync("api/Invitations/multiple", dto);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<BulkInvitationResponseDTO>();

                    if (result != null)
                    {
                        var successMessage = $"Successfully sent {result.SuccessfulCount} invitation(s).";
                        if (result.FailedCount > 0)
                        {
                            successMessage += $" {result.FailedCount} invitation(s) failed.";

                            // Add details about failed invitations
                            if (result.FailedInvitations.Any())
                            {
                                var failedDetails = string.Join("; ",
                                    result.FailedInvitations.Select(f => $"{f.Email}: {f.Error}"));
                                successMessage += $" Failed: {failedDetails}";
                            }
                        }

                        Message = successMessage;

                        // Clear the form if all were successful
                        if (result.FailedCount == 0)
                        {
                            InviteEmails = string.Empty;
                        }
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Message = $"Failed to send invitations: {response.StatusCode} - {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending invitations");
                Message = "An error occurred while sending invitations.";
            }

            await OnGetAsync();
            return Page();
        }

        // NEW: Load existing invitations
        private async Task LoadInvitations()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/Invitations");
                if (response.IsSuccessStatusCode)
                {
                    Invitations = await response.Content.ReadFromJsonAsync<List<InvitationResponseDTO>>() ?? new List<InvitationResponseDTO>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invitations");
                // Continue without invitations - it's not critical
            }
        }

        // NEW: Handle resending invitation
        public async Task<IActionResult> OnPostResendInvitationAsync(string id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/Invitations/resend/{id}", null);
                if (response.IsSuccessStatusCode)
                {
                    Message = "Invitation resent successfully.";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Message = $"Failed to resend invitation: {response.StatusCode} - {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending invitation {Id}", id);
                Message = "An error occurred while resending the invitation.";
            }

            return RedirectToPage("Index", new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });
        }

        // NEW: Handle deleting invitation
        public async Task<IActionResult> OnPostDeleteInvitationAsync(string id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/Invitations/{id}");
                if (response.IsSuccessStatusCode)
                {
                    Message = "Invitation deleted successfully.";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Message = $"Failed to delete invitation: {response.StatusCode} - {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invitation {Id}", id);
                Message = "An error occurred while deleting the invitation.";
            }

            return RedirectToPage("Index", new { page = CurrentPage, search = Search, departmentId = DepartmentId, isActive = IsActive });
        }

        // NEW: Email validation helper
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        public class DepartmentReadDTO
        {
            public string DepartmentId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }
    }
}