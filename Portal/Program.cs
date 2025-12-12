
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Portal.Handlers;
using Portal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserRoleService, UserRoleService>();
builder.Services.AddScoped<IApiAuthService, ApiAuthService>();
builder.Services.AddTransient<TokenRefreshHandler>();
builder.Services.AddScoped<UnauthorizedRedirectHandler>();

// IMPORTANT: Separate HttpClient for auth operations (no handlers to avoid circular dependencies)
builder.Services.AddHttpClient("AuthApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://mugassetapi.runasp.net/");
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Main HttpClient with handlers for regular API calls
// CRITICAL: TokenRefreshHandler MUST come BEFORE UnauthorizedRedirectHandler
builder.Services.AddHttpClient("AssetTagApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://mugassetapi.runasp.net/");
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(30);
})
    .AddHttpMessageHandler<TokenRefreshHandler>();        // First: Handle token refresh
                                                          //.AddHttpMessageHandler<UnauthorizedRedirectHandler>(); // Then: Handle redirect if still unauthorized

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
        options.Cookie.HttpOnly = false;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.None; // Allow cookies to be sent on redirects
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true; // Refresh cookie expiration on each request
        options.Cookie.IsEssential = true;
    });

// Configure anti - forgery for production
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.Name = "AntiForgeryToken";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
    options.SuppressXFrameOptionsHeader = false;
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToFolder("/Account");
    options.Conventions.AllowAnonymousToPage("/Unauthorized");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.Run();


