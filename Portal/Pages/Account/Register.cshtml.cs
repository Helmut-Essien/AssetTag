using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Services;
using Shared.DTOs;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json;

namespace Portal.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly IApiAuthService _authService;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public RegisterModel(IApiAuthService authService, ILogger<RegisterModel> logger, IHttpClientFactory httpClientFactory)
        {
            _authService = authService;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public string? TokenErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsValidToken { get; set; } = false;
        public bool IsSuccess { get; set; } = false;
        public string? InvitationRole { get; set; }

        public class InputModel
        {
            [Required]
            public string Token { get; set; } = string.Empty;

            [Required(ErrorMessage = "Username is required.")]
            [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
            [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores.")]
            public string Username { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email address.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "First name is required.")]
            [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
            public string FirstName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Surname is required.")]
            [StringLength(50, ErrorMessage = "Surname cannot exceed 50 characters.")]
            public string Surname { get; set; } = string.Empty;

            [StringLength(100, ErrorMessage = "Other names cannot exceed 100 characters.")]
            public string? OtherNames { get; set; }

            [Required(ErrorMessage = "Password is required.")]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Confirm password is required.")]
            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [StringLength(100, ErrorMessage = "Job role cannot exceed 100 characters.")]
            public string? JobRole { get; set; }

            public DateTime? DateOfBirth { get; set; }
            public string? Address { get; set; }
            public string? DepartmentId { get; set; }
        }

        private HttpClient CreateApiClient()
        {
            return _httpClientFactory.CreateClient("AssetTagApi");
        }

        public async Task<IActionResult> OnGetAsync(string? token = null)
        {
            if (string.IsNullOrEmpty(token))
            {
                TokenErrorMessage = "Invalid invitation link. Please check your email for the correct link or request a new invitation.";
                return Page();
            }

            try
            {
                _logger.LogInformation("Validating invitation token: {Token}", token);

                using var client = CreateApiClient();
                var response = await client.GetAsync($"api/Invitations/validate/{token}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var validationResult = JsonSerializer.Deserialize<InvitationValidationResult>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (validationResult != null && validationResult.Success)
                    {
                        Input.Token = token;
                        Input.Email = validationResult.Data?.Email ?? string.Empty;
                        InvitationRole = validationResult.Data?.Role;
                        IsValidToken = true;

                        _logger.LogInformation("Valid invitation token for email: {Email}", Input.Email);
                    }
                    else
                    {
                        TokenErrorMessage = validationResult?.Message ?? "Invalid or expired invitation token. Please request a new invitation.";
                        _logger.LogWarning("Invalid invitation token: {Token}, Message: {Message}",
                            token, validationResult?.Message);
                        return Page();
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("API error validating token: {StatusCode} - {Error}", response.StatusCode, errorContent);

                    try
                    {
                        var errorResult = JsonSerializer.Deserialize<InvitationValidationResult>(
                            errorContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        TokenErrorMessage = errorResult?.Message ?? "Failed to validate invitation. Please try again.";
                    }
                    catch
                    {
                        TokenErrorMessage = "Failed to validate invitation. Please try again.";
                    }
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating invitation token: {Token}, Error: {ErrorMessage}",
                    token, ex.Message);
                TokenErrorMessage = $"An error occurred while validating your invitation: {ex.Message}. Please try again or request a new invitation.";
                return Page();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model validation failed for registration");

                // Re-validate token on post and set IsValidToken
                try
                {
                    using var client = CreateApiClient();
                    var response = await client.GetAsync($"api/Invitations/validate/{Input.Token}");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var validationResult = JsonSerializer.Deserialize<InvitationValidationResult>(
                            content,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        IsValidToken = validationResult != null && validationResult.Success;
                        if (IsValidToken)
                        {
                            InvitationRole = validationResult.Data?.Role;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error re-validating token during POST: {Token}", Input.Token);
                    IsValidToken = false;
                }
                return Page();
            }

            try
            {
                _logger.LogInformation("Processing registration for email: {Email}", Input.Email);

                // Re-validate token before registration
                using var validationClient = CreateApiClient();
                var validationResponse = await validationClient.GetAsync($"api/Invitations/validate/{Input.Token}");
                if (!validationResponse.IsSuccessStatusCode)
                {
                    var errorContent = await validationResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("Token validation failed during registration: {StatusCode} - {Error}",
                        validationResponse.StatusCode, errorContent);

                    TokenErrorMessage = "Invalid or expired invitation token. Please request a new invitation.";
                    IsValidToken = false;
                    return Page();
                }

                var content = await validationResponse.Content.ReadAsStringAsync();
                var validationResult = JsonSerializer.Deserialize<InvitationValidationResult>(
                    content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (validationResult == null || !validationResult.Success)
                {
                    TokenErrorMessage = validationResult?.Message ?? "Invalid or expired invitation token. Please request a new invitation.";
                    IsValidToken = false;
                    return Page();
                }

                // Ensure email matches invitation
                if (!string.Equals(validationResult.Data?.Email, Input.Email, StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = "Email does not match the invitation. Please use the email address that received the invitation.";
                    IsValidToken = true;
                    InvitationRole = validationResult.Data?.Role;
                    return Page();
                }

                var registerDto = new RegisterWithInvitationDTO
                {
                    Token = Input.Token,
                    Username = Input.Username,
                    Email = Input.Email,
                    Password = Input.Password,
                    ConfirmPassword = Input.ConfirmPassword,
                    FirstName = Input.FirstName,
                    Surname = Input.Surname,
                    OtherNames = Input.OtherNames,
                    DateOfBirth = Input.DateOfBirth,
                    Address = Input.Address,
                    JobRole = Input.JobRole,
                    DepartmentId = Input.DepartmentId
                };

                // Use the auth service for registration
                var result = await _authService.RegisterWithInvitationAsync(registerDto);

                if (result != null && result.Success)
                {
                    SuccessMessage = result.Message ?? "Registration successful! You can now login with your credentials.";
                    IsSuccess = true;
                    IsValidToken = false;

                    // Clear the form
                    ModelState.Clear();
                    Input = new InputModel();

                    _logger.LogInformation("Successful registration with invitation for email: {Email}", Input.Email);
                }
                else
                {
                    ErrorMessage = result?.Message ?? "Failed to register. Please check your information and try again.";
                    IsValidToken = true;
                    InvitationRole = validationResult.Data?.Role;

                    _logger.LogWarning("Registration failed for email: {Email}, Error: {Error}", Input.Email, ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during registration with invitation for {Email}", Input.Email);
                ErrorMessage = $"An error occurred during registration: {ex.Message}. Please try again.";

                // Re-validate token
                try
                {
                    using var client = CreateApiClient();
                    var response = await client.GetAsync($"api/Invitations/validate/{Input.Token}");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var validationResult = JsonSerializer.Deserialize<InvitationValidationResult>(
                            content,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        IsValidToken = validationResult != null && validationResult.Success;
                        if (IsValidToken)
                        {
                            InvitationRole = validationResult.Data?.Role;
                        }
                    }
                }
                catch
                {
                    IsValidToken = false;
                }
            }

            return Page();
        }
    }
}






//using Microsoft.AspNetCore.Authentication;
//using Microsoft.AspNetCore.Authentication.Cookies;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.RazorPages;
//using Portal.Services;
//using Shared.DTOs;
//using System.ComponentModel.DataAnnotations;
//using System.Security.Claims;

//namespace Portal.Pages.Account
//{
//    public class RegisterModel : PageModel
//    {
//        private readonly IApiAuthService _authService;

//        public RegisterModel(IApiAuthService authService)
//        {
//            _authService = authService;
//        }

//        [BindProperty]
//        public InputModel Input { get; set; } = new InputModel();

//        public string? ErrorMessage { get; set; }
//        public string? SuccessMessage { get; set; }

//        public class InputModel
//        {
//            [Required(ErrorMessage = "Username is required.")]
//            [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
//            [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores.")]
//            public string Username { get; set; } = string.Empty;

//            [Required(ErrorMessage = "Email is required.")]
//            [EmailAddress(ErrorMessage = "Invalid email address.")]
//            public string Email { get; set; } = string.Empty;

//            [Required(ErrorMessage = "First name is required.")]
//            [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
//            public string FirstName { get; set; } = string.Empty;

//            [Required(ErrorMessage = "Surname is required.")]
//            [StringLength(50, ErrorMessage = "Surname cannot exceed 50 characters.")]
//            public string Surname { get; set; } = string.Empty;

//            [Required(ErrorMessage = "Password is required.")]
//            [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
//            [DataType(DataType.Password)]
//            public string Password { get; set; } = string.Empty;

//            [Required(ErrorMessage = "Confirm password is required.")]
//            [DataType(DataType.Password)]
//            [Compare("Password", ErrorMessage = "Passwords do not match.")]
//            public string ConfirmPassword { get; set; } = string.Empty;
//        }

//        public void OnGet()
//        {
//        }

//        public async Task<IActionResult> OnPostAsync()
//        {
//            if (!ModelState.IsValid)
//            {
//                var errors = ModelState.Values
//                    .SelectMany(v => v.Errors)
//                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.Exception?.Message : e.ErrorMessage)
//                    .Where(m => !string.IsNullOrWhiteSpace(m))
//                    .ToArray();

//                ErrorMessage = errors.Length == 0 ? "Form validation failed." : string.Join(" ", errors);
//                return Page();
//            }

//            try
//            {
//                // Create RegisterDTO from input
//                var registerDto = new RegisterDTO(
//                    Input.Username,
//                    Input.Email,
//                    Input.Password,
//                    Input.FirstName,
//                    Input.Surname
//                );

//                // Call the API to register
//                var result = await _authService.RegisterAsync(registerDto);
//                if (!result)
//                {
//                    ErrorMessage = "Registration failed. Please try again.";
//                    return Page();
//                }

//                SuccessMessage = "Registration successful! You can now sign in.";

//                // Clear form
//                Input = new InputModel();

//                // Optionally auto-login after registration
//                // return await AutoLoginAfterRegistration(registerDto);

//                return Page();
//            }
//            catch (Exception ex)
//            {
//                ErrorMessage = "An error occurred during registration. Please try again.";
//                // Log the exception in a real application
//                return Page();
//            }
//        }

//        // Optional: Auto-login after successful registration
//        private async Task<IActionResult> AutoLoginAfterRegistration(RegisterDTO registerDto)
//        {
//            var loginDto = new LoginDTO(registerDto.Email, registerDto.Password);
//            var tokenResponse = await _authService.LoginAsync(loginDto);

//            if (tokenResponse == null)
//            {
//                SuccessMessage = "Registration successful! Please sign in.";
//                return Page();
//            }

//            // Create claims and sign in
//            var claims = new List<Claim>
//            {
//                new Claim(ClaimTypes.Name, registerDto.Email),
//                new Claim("AccessToken", tokenResponse.AccessToken),
//                new Claim("RefreshToken", tokenResponse.RefreshToken)
//            };

//            var claimsIdentity = new ClaimsIdentity(claims, "PortalCookie");
//            var authProperties = new AuthenticationProperties
//            {
//                IsPersistent = true,
//                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
//            };

//            await HttpContext.SignInAsync(
//                "PortalCookie",
//                new ClaimsPrincipal(claimsIdentity),
//                authProperties);

//            return RedirectToPage("/Index");
//        }
//    }
//}