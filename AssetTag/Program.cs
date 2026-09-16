using AssetTag.Data;
using AssetTag.Filters;
using AssetTag.HealthChecks;
using AssetTag.Middleware;
using Shared.Models;
using AssetTag.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Globalization;


var builder = WebApplication.CreateBuilder(args);

// Configure globalization for Ghana Cedis (GHS)
var ghanaianCulture = new CultureInfo("en-GH");
ghanaianCulture.NumberFormat.CurrencySymbol = "₵";
ghanaianCulture.NumberFormat.CurrencyDecimalDigits = 2;

CultureInfo.DefaultThreadCurrentCulture = ghanaianCulture;
CultureInfo.DefaultThreadCurrentUICulture = ghanaianCulture;

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(
    builder.Environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection") + ";MultipleActiveResultSets=true",
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        }));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.User.RequireUniqueEmail= true;
    options.Password.RequiredLength = 6;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(Shared.Constants.EmailConstants.PasswordResetExpiryHours);
});

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.UTF8.GetBytes(jwtSettings["SecurityKey"]!);

if (builder.Environment.IsProduction())
{
    var keysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Keys");
    var keysDirectory = new DirectoryInfo(keysPath);

    if (!keysDirectory.Exists)
    {
        keysDirectory.Create(); // Creates the folder at runtime if missing (e.g. first deploy)
    }

    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(keysDirectory)
        .SetApplicationName("AssetTag")
        .SetDefaultKeyLifetime(TimeSpan.FromDays(90)); // Optional: keys rotate every 90 days
        //.ProtectKeysWithDpapi();
}

// JWT Authentication (security-stamp + role refresh on each validated token)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.FromMinutes(5)
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(
                context.Exception,
                "JWT authentication failed for {Method} {Path}: {Reason}",
                context.Request.Method,
                context.Request.Path,
                context.Exception.GetType().Name);
            return Task.CompletedTask;
        },

        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogDebug(
                "JWT challenge (401) for {Method} {Path} (error={Error})",
                context.Request.Method,
                context.Request.Path,
                context.Error);
            return Task.CompletedTask;
        },

        // Validate security stamp so role/password changes invalidate outstanding access tokens
        OnTokenValidated = async context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

            var userId = context.Principal?.FindFirst("sub")?.Value
                        ?? context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                logger.LogWarning("Token validated but missing user id claim; rejecting");
                context.Fail("Missing user identifier.");
                return;
            }

            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            if (user is null || !user.IsActive)
            {
                logger.LogWarning("Token rejected for {UserId}: user missing or inactive", userId);
                context.Fail("User is inactive or no longer exists.");
                return;
            }

            var tokenStamp = context.Principal?.FindFirst("security_stamp")?.Value;
            if (string.IsNullOrEmpty(tokenStamp) ||
                !string.Equals(tokenStamp, user.SecurityStamp, StringComparison.Ordinal))
            {
                logger.LogWarning("Token rejected for {UserId}: security stamp mismatch (roles/credentials changed)", userId);
                context.Fail("Security stamp mismatch.");
                return;
            }

            // Keep authorization roles aligned with the database (defense in depth vs stale JWT role claims)
            var dbRoles = await userManager.GetRolesAsync(user);
            var identity = context.Principal?.Identity as ClaimsIdentity;
            if (identity != null)
            {
                foreach (var existing in identity.FindAll(ClaimTypes.Role).ToList())
                {
                    identity.RemoveClaim(existing);
                }
                foreach (var existing in identity.FindAll("role").ToList())
                {
                    identity.RemoveClaim(existing);
                }
                foreach (var role in dbRoles)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }

            logger.LogDebug("JWT validated for user {UserId}", userId);
        },

        OnForbidden = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(
                "JWT forbidden (403) for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
            return Task.CompletedTask;
        }
    };
});

// Or use named client approach:
builder.Services.AddHttpClient("GroqClient", client =>
{
    client.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Register AI Service
builder.Services.AddScoped<IAIQueryService, AIQueryService>();



// Add custom user validator to check IsActive status

builder.Services.AddScoped<ActiveUserAttribute>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.Configure<EmailService.EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<EmailBackgroundQueue>();
builder.Services.AddSingleton<IEmailBackgroundQueue>(sp => sp.GetRequiredService<EmailBackgroundQueue>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<EmailBackgroundQueue>());

// ARCHITECTURAL FIX A1: Register distributed lock service for multi-device sync coordination
builder.Services.AddScoped<IDistributedLockService, DatabaseDistributedLockService>();
builder.Services.AddScoped<IAssetImportService, AssetImportService>();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

builder.Configuration.AddEnvironmentVariables();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "OpenAPI V1");
    });
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                message = "An unexpected error occurred."
            });
        });
    });
    app.UseHsts();
}

// Seed built-in roles and initial admin (only creates admin when DB has no users)
try
{
    using var scope = app.Services.CreateScope();
    await SeedData.InitializeAsync(scope.ServiceProvider, app.Environment, app.Configuration);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while seeding the database.");

    // Fail fast outside Development so empty production DBs without InitialAdmin
    // do not start as an unmanageable API with no admin users.
    if (!app.Environment.IsDevelopment())
    {
        throw;
    }
}

app.UseHttpsRedirection();

// Map mobile X-Auth-Token → Authorization (must run before authentication)
app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-Auth-Token", out var customToken) &&
        !context.Request.Headers.ContainsKey("Authorization"))
    {
        context.Request.Headers["Authorization"] = customToken.ToString();
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteMinimalHealthResponse
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteMinimalHealthResponse
}).AllowAnonymous();

app.MapControllers();

app.Run();

static Task WriteMinimalHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString()
    });
}
