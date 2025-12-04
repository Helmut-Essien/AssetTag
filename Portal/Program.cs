using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Net.Http.Headers;
using Portal.Handlers;
using Portal.Services;

var builder = WebApplication.CreateBuilder(args);

// register typed HttpClient for API calls (set Api:BaseUrl in Portal appsettings)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserRoleService, UserRoleService>();
builder.Services.AddScoped<IApiAuthService, ApiAuthService>();
builder.Services.AddTransient<TokenRefreshHandler>();
builder.Services.AddScoped<UnauthorizedRedirectHandler>();

// Separate HttpClient for auth operations (no handlers)
builder.Services.AddHttpClient("AuthApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7135/");
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

// Main HttpClient with handlers for regular API calls
builder.Services.AddHttpClient("AssetTagApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7135/");
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
})
    .AddHttpMessageHandler<UnauthorizedRedirectHandler>()// <--- attach handler here
    .AddHttpMessageHandler<Portal.Services.TokenRefreshHandler>();




// cookie auth for portal users
builder.Services.AddAuthentication("PortalCookie")
    .AddCookie("PortalCookie", options =>
    {
        options.Cookie.Name = "PortalAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

//builder.Services.AddRazorPages();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");          // all pages require login
    options.Conventions.AllowAnonymousToFolder("/Account"); // except login/register pages
    options.Conventions.AllowAnonymousToPage("/Unauthorized"); // allow unauthorized page
});


var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.Run();
