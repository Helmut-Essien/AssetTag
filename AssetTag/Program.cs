using AssetTag.Data;
using AssetTag.Filters;
using AssetTag.Models;
using AssetTag.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Trace);   // or LogLevel.Debug

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
        // === ADD THIS ENTIRE BLOCK ===
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

                logger.LogWarning(context.Exception,
                    "JWT authentication failed for request {Method} {Path}. Reason: {Message}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Exception.Message);

                if (context.Exception is SecurityTokenExpiredException)
                {
                    logger.LogWarning("Token has expired.");
                }
                else if (context.Exception.Message.Contains("not yet valid"))
                {
                    logger.LogWarning("Token is not yet valid (clock skew suspected).");
                }
                else if (context.Exception is SecurityTokenInvalidSignatureException)
                {
                    logger.LogWarning("Invalid token signature.");
                }

                return Task.CompletedTask;
            },

            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                // Get the email claim (standard claim type: "email")
                var email = context.Principal?.FindFirst("email")?.Value
                            ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                            ?? "unknown";

                // Optional: also get UserId if you still want it
                var userId = context.Principal?.FindFirst("sub")?.Value ?? "unknown";

                logger.LogInformation("JWT token successfully validated for user {Email}",
                    email);
                return Task.CompletedTask;
            }
        };
        // === END OF ADDITION ===
    });

// Add custom user validator to check IsActive status

builder.Services.AddScoped<ActiveUserAttribute>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.Configure<EmailService.EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Configuration.AddEnvironmentVariables();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
    options.SwaggerEndpoint("/openapi/v1.json", "OpenAPI V1");

    });
}

// Seed initial admin user (only runs once)
try
{
    using var scope = app.Services.CreateScope();
    await SeedData.InitializeAsync(scope.ServiceProvider, app.Environment, app.Configuration);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while seeding the database.");
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
