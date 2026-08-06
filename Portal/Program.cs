
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Portal.Handlers;
using Portal.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Configure globalization for Ghana Cedis (GHS)
var ghanaianCulture = new CultureInfo("en-GH");
ghanaianCulture.NumberFormat.CurrencySymbol = "₵";
ghanaianCulture.NumberFormat.CurrencyDecimalDigits = 2;

CultureInfo.DefaultThreadCurrentCulture = ghanaianCulture;
CultureInfo.DefaultThreadCurrentUICulture = ghanaianCulture;

// Secure cookies in Production; SameAsRequest so local HTTP (launch profile) works.
var cookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "Portal.Session";
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserRoleService, UserRoleService>();
builder.Services.AddScoped<IApiAuthService, ApiAuthService>();
builder.Services.AddTransient<TokenRefreshHandler>();
builder.Services.AddScoped<UnauthorizedRedirectHandler>();

// Api:BaseUrl is first-class env config (appsettings / user secrets / CI vars.API_BASE_URL).
var apiBaseUrl = builder.Configuration["Api:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
{
    throw new InvalidOperationException(
        "Api:BaseUrl is not configured. " +
        "Local: set Api:BaseUrl in appsettings.Development.json or user secrets. " +
        "CI: set repository secret API_BASE_URL.");
}
var apiBaseUri = new Uri(apiBaseUrl.EndsWith('/') ? apiBaseUrl : apiBaseUrl + "/");

// IMPORTANT: Separate HttpClient for auth operations (no handlers to avoid circular dependencies)
builder.Services.AddHttpClient("AuthApi", client =>
{
    client.BaseAddress = apiBaseUri;
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Main HttpClient with handlers for regular API calls
// CRITICAL: TokenRefreshHandler MUST come BEFORE UnauthorizedRedirectHandler
builder.Services.AddHttpClient("AssetTagApi", client =>
{
    client.BaseAddress = apiBaseUri;
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(30);
})
    .AddHttpMessageHandler<TokenRefreshHandler>();        // First: Handle token refresh

// ADD THIS: Create Reports Service
builder.Services.AddScoped<IReportsService, ReportsService>();                                                       //.AddHttpMessageHandler<UnauthorizedRedirectHandler>(); // Then: Handle redirect if still unauthorized

if (builder.Environment.IsProduction())
{
    var keysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Keys");
    var keysDirectory = new DirectoryInfo(keysPath);

    if (!keysDirectory.Exists)
    {
        keysDirectory.Create();
    }

    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(keysDirectory)
        .SetApplicationName("AssetTag")
        .SetDefaultKeyLifetime(TimeSpan.FromDays(90));
}

// Cookie authentication for portal users
builder.Services.AddAuthentication("PortalCookie")
    .AddCookie("PortalCookie", options =>
    {
        options.Cookie.Name = "PortalAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
        options.Cookie.SameSite = SameSiteMode.Lax; // Allow cookies to be sent on redirects
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true; // Refresh cookie expiration on each request
        options.Cookie.IsEssential = true;
    });

// Accept antiforgery token from AJAX header (Users /Assets page scripts send this name)
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.Name = "Portal.AntiForgery";
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToFolder("/Account");
    options.Conventions.AllowAnonymousToPage("/Unauthorized");
    options.Conventions.AllowAnonymousToPage("/Forbidden");
    options.Conventions.AllowAnonymousToPage("/Error");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// ADD THIS LINE: Use session middleware (must come before UseRouting)
app.UseSession();

app.UseRouting();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.Run();


