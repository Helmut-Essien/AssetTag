using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Portal.Pages.Categories
{
    public class IndexModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
        {
            _httpClient = httpClientFactory.CreateClient("AssetTagApi"); // Assume named client configured with base address and auth
            _logger = logger;
        }

        public List<CategoryReadDTO> Categories { get; set; } = new();

        [BindProperty]
        public CategoryCreateDTO CreateDto { get; set; } = new CategoryCreateDTO();

        [BindProperty]
        public CategoryUpdateDTO UpdateDto { get; set; } = new CategoryUpdateDTO();

        public string? ActiveModal { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            _logger.LogInformation("OnGetAsync: requesting GET api/categories.");

            var response = await _httpClient.GetAsync("api/categories");

            _logger.LogInformation("OnGetAsync: response status {StatusCode}.", response.StatusCode);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("OnGetAsync: unauthorized (401) returned from API.");
                return Challenge();
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("OnGetAsync: failed to load categories. Status: {Status}, Body: {Body}", response.StatusCode, body);
                ModelState.AddModelError(string.Empty, $"Unable to load categories. Server returned {(int)response.StatusCode}.");
                Categories = new List<CategoryReadDTO>();
                return Page();
            }

            Categories = await response.Content.ReadFromJsonAsync<List<CategoryReadDTO>>() ?? new List<CategoryReadDTO>();
            _logger.LogInformation("OnGetAsync: loaded {Count} categories.", Categories.Count);
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            _logger.LogInformation("OnPostCreateAsync: creating category. Payload: {Payload}", JsonSerializer.Serialize(CreateDto));

            if (!ModelState.IsValid)
            {
                _logger.LogInformation("OnPostCreateAsync: model state invalid.");
                ActiveModal = "create";
                await OnGetAsync();
                return Page();
            }

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync("api/categories", CreateDto);
            }
            catch (System.Exception ex)
            {
                _logger.LogInformation("OnPostCreateAsync: exception while calling API: {Exception}", ex);
                ModelState.AddModelError(string.Empty, "Unable to call API to create category.");
                ActiveModal = "create";
                await OnGetAsync();
                return Page();
            }

            _logger.LogInformation("OnPostCreateAsync: API responded {StatusCode}.", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("OnPostCreateAsync: category created successfully.");
                return RedirectToPage();
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("OnPostCreateAsync: create failed. Status: {Status}, Body: {Body}", response.StatusCode, responseBody);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("OnPostCreateAsync: Unauthorized (401) - user may not be authenticated or token missing/expired.");
                ModelState.AddModelError(string.Empty, "Not authorized to create category. Please sign in.");
            }
            else if (response.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogInformation("OnPostCreateAsync: Conflict (409) - name exists.");
                ModelState.AddModelError("CreateDto.Name", "Category name already exists.");
            }
            else
            {
                _logger.LogInformation("OnPostCreateAsync: unexpected failure creating category: {Status} - {Reason}", (int)response.StatusCode, response.ReasonPhrase);
                ModelState.AddModelError(string.Empty, $"Create failed: {(int)response.StatusCode} - {response.ReasonPhrase}");
            }

            ActiveModal = "create";
            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync()
        {
            _logger.LogInformation("OnPostUpdateAsync: updating category {Id} with payload: {Payload}", UpdateDto.CategoryId, JsonSerializer.Serialize(UpdateDto));

            if (string.IsNullOrEmpty(UpdateDto.CategoryId))
            {
                _logger.LogInformation("OnPostUpdateAsync: category id missing.");
                ActiveModal = "edit";
                await OnGetAsync();
                return Page();
            }

            var response = await _httpClient.PutAsJsonAsync($"api/categories/{UpdateDto.CategoryId}", UpdateDto);
            _logger.LogInformation("OnPostUpdateAsync: API responded {StatusCode}.", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("OnPostUpdateAsync: update successful.");
                return RedirectToPage();
            }
                            
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogInformation("OnPostUpdateAsync: Conflict - name exists.");
                ModelState.AddModelError("UpdateDto.Name", "Category name already exists.");
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("OnPostUpdateAsync: NotFound - category {Id} not found.", UpdateDto.CategoryId);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("OnPostUpdateAsync: failed. Status: {Status}, Body: {Body}", response.StatusCode, body);
            }

            ActiveModal = "edit";
            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            _logger.LogInformation("OnPostDeleteAsync: deleting category {Id}.", id);

            if (string.IsNullOrEmpty(id))
            {
                _logger.LogInformation("OnPostDeleteAsync: id was empty.");
                return RedirectToPage();
            }

            var response = await _httpClient.DeleteAsync($"api/categories/{id}");
            _logger.LogInformation("OnPostDeleteAsync: API responded {StatusCode}.", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("OnPostDeleteAsync: delete successful.");
                return RedirectToPage();
            }

            var respBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("OnPostDeleteAsync: failed to delete. Status: {Status}, Body: {Body}", response.StatusCode, respBody);

            await OnGetAsync();
            return Page();
        }
    }
}