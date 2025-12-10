//using Microsoft.AspNetCore.Authentication.Cookies;
//using Microsoft.AspNetCore.DataProtection;
//using Microsoft.Net.Http.Headers;
//using Portal.Handlers;
//using Portal.Services;

//var builder = WebApplication.CreateBuilder(args);

//// register typed HttpClient for API calls (set Api:BaseUrl in Portal appsettings)
//builder.Services.AddHttpContextAccessor();
//builder.Services.AddScoped<IUserRoleService, UserRoleService>();
//builder.Services.AddScoped<IApiAuthService, ApiAuthService>();
//builder.Services.AddTransient<TokenRefreshHandler>();
//builder.Services.AddScoped<UnauthorizedRedirectHandler>();

//// Separate HttpClient for auth operations (no handlers)
//builder.Services.AddHttpClient("AuthApi", client =>
//{
//    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://mugassetapi.runasp.net/");
//    client.DefaultRequestHeaders.Accept.Clear();
//    client.DefaultRequestHeaders.Accept.Add(
//        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
//});

//// Main HttpClient with handlers for regular API calls
//builder.Services.AddHttpClient("AssetTagApi", client =>
//{
//    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://mugassetapi.runasp.net/");
//    client.DefaultRequestHeaders.Accept.Clear();
//    client.DefaultRequestHeaders.Accept.Add(
//        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
//})
//    .AddHttpMessageHandler<UnauthorizedRedirectHandler>()// <--- attach handler here
//    .AddHttpMessageHandler<Portal.Services.TokenRefreshHandler>();

//if (builder.Environment.IsProduction())
//{
//    var keysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Keys");
//    var keysDirectory = new DirectoryInfo(keysPath);

//    if (!keysDirectory.Exists)
//    {
//        keysDirectory.Create(); // Creates the folder at runtime if missing (e.g. first deploy)
//    }

//    builder.Services.AddDataProtection()
//        .PersistKeysToFileSystem(keysDirectory)
//        .SetApplicationName("AssetTag")
//        .SetDefaultKeyLifetime(TimeSpan.FromDays(90));// Optional: keys rotate every 90 days
//        //.ProtectKeysWithDpapi(); // Add this for Windows hosting
//}


//// cookie auth for portal users
//builder.Services.AddAuthentication("PortalCookie")
//    .AddCookie("PortalCookie", options =>
//    {
//        options.Cookie.Name = "PortalAuth";
//        options.Cookie.HttpOnly = true;
//        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
//        options.LoginPath = "/Account/Login";
//        options.ExpireTimeSpan = TimeSpan.FromHours(8);
//    });

////builder.Services.AddRazorPages();
//builder.Services.AddRazorPages(options =>
//{
//    options.Conventions.AuthorizeFolder("/");          // all pages require login
//    options.Conventions.AllowAnonymousToFolder("/Account"); // except login/register pages
//    options.Conventions.AllowAnonymousToPage("/Unauthorized"); // allow unauthorized page
//});


//var app = builder.Build();

//app.UseHttpsRedirection();
//app.UseStaticFiles();

//app.UseRouting();
//app.UseAuthentication();
//app.UseAuthorization();

//app.MapRazorPages();
//app.Run();

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
        options.Cookie.SameSite = SameSiteMode.Lax; // Allow cookies to be sent on redirects
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true; // Refresh cookie expiration on each request
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
