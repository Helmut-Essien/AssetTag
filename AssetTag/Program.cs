using AssetTag.Data;
using AssetTag.Filters;
using AssetTag.Models;
using AssetTag.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

//builder.Services.AddAuthentication(options =>
//{
//    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
//    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
//})
//    .AddJwtBearer(options =>
//    {

//        options.TokenValidationParameters = new TokenValidationParameters
//        {
//            ValidateIssuer = true,
//            ValidateAudience = true,
//            ValidateLifetime = true,
//            ValidateIssuerSigningKey = true,
//            ValidIssuer = jwtSettings["Issuer"],
//            ValidAudience = jwtSettings["Audience"],
//            IssuerSigningKey = new SymmetricSecurityKey(key),
//            ClockSkew = TimeSpan.FromMinutes(5)
//        };
//        // === ADD THIS ENTIRE BLOCK ===
//        options.Events = new JwtBearerEvents
//        {
//            OnAuthenticationFailed = context =>
//            {
//                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

//                logger.LogWarning(context.Exception,
//                    "JWT authentication failed for request {Method} {Path}. Reason: {Message}",
//                    context.Request.Method,
//                    context.Request.Path,
//                    context.Exception.Message);

//                if (context.Exception is SecurityTokenExpiredException)
//                {
//                    logger.LogWarning("Token has expired.");
//                }
//                else if (context.Exception.Message.Contains("not yet valid"))
//                {
//                    logger.LogWarning("Token is not yet valid (clock skew suspected).");
//                }
//                else if (context.Exception is SecurityTokenInvalidSignatureException)
//                {
//                    logger.LogWarning("Invalid token signature.");
//                }

//                return Task.CompletedTask;
//            },

//            OnChallenge = context =>
//            {
//                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

//                // Log that a 401 is being issued
//                logger.LogWarning("JWT challenge triggered (401 issued) for request {Method} {Path}",
//                    context.Request.Method,
//                    context.Request.Path);

//                // Check if there was an authentication failure (token present but invalid)
//                if (context.AuthenticateFailure != null)
//                {
//                    logger.LogWarning("Authentication failed with exception: {Type} - {Message}",
//                        context.AuthenticateFailure.GetType().Name,
//                        context.AuthenticateFailure.Message);

//                    if (context.AuthenticateFailure is SecurityTokenExpiredException)
//                    {
//                        logger.LogWarning("Reason: Token has expired.");
//                    }
//                    else if (context.AuthenticateFailure.Message.Contains("not yet valid"))
//                    {
//                        logger.LogWarning("Reason: Token is not yet valid (possible clock skew).");
//                    }
//                    else if (context.AuthenticateFailure is SecurityTokenInvalidSignatureException)
//                    {
//                        logger.LogWarning("Reason: Invalid token signature.");
//                    }
//                }
//                else
//                {
//                    // No AuthenticateFailure means: no token, malformed header, or scheme mismatch
//                    logger.LogWarning("No token provided or Authorization header is missing/malformed.");
//                }

//                return Task.CompletedTask;
//            },

//            OnTokenValidated = context =>
//            {
//                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
//                // Get the email claim (standard claim type: "email")
//                var email = context.Principal?.FindFirst("email")?.Value
//                            ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
//                            ?? "unknown";

//                // Optional: also get UserId if you still want it
//                var userId = context.Principal?.FindFirst("sub")?.Value ?? "unknown";

//                logger.LogInformation("JWT token successfully validated for user {Email}",
//                    email);
//                return Task.CompletedTask;
//            }
//        };
//        // === END OF ADDITION ===
//    });

// JWT Authentication with Enhanced Logging
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
        // Log when a message is received (fires first)
        OnMessageReceived = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

            // Log request details
            logger.LogInformation("=== JWT MESSAGE RECEIVED ===");
            logger.LogInformation("Request: {Method} {Path}", context.Request.Method, context.Request.Path);
            logger.LogInformation("Request ID: {TraceIdentifier}", context.HttpContext.TraceIdentifier);

            // Check for Authorization header
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

            if (string.IsNullOrEmpty(authHeader))
            {
                logger.LogWarning("NO Authorization header found");

                // Log ALL headers for debugging
                logger.LogDebug("=== All Request Headers ===");
                foreach (var header in context.Request.Headers)
                {
                    logger.LogDebug("Header: {Key} = {Value}", header.Key, header.Value);
                }

                // Check for cookies (if using cookie fallback)
                var authCookie = context.Request.Cookies["Authorization"];
                if (!string.IsNullOrEmpty(authCookie))
                {
                    logger.LogInformation("Found Authorization cookie: {CookieLength} chars", authCookie.Length);
                }
            }
            else
            {
                logger.LogInformation("Authorization header found: {HeaderFirst100}...",
                    authHeader.Length > 100 ? authHeader.Substring(0, 100) : authHeader);

                // Validate header format
                if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogError("Authorization header doesn't start with 'Bearer '. Actual: '{HeaderPrefix}'...",
                        authHeader.Length > 10 ? authHeader.Substring(0, 10) : authHeader);
                }
                else
                {
                    var token = authHeader.Substring(7); // Remove "Bearer "

                    // Log token basics (don't log full token for security)
                    logger.LogInformation("Token length: {Length} characters", token.Length);
                    logger.LogInformation("Token starts with: {First20}...", token.Length > 20 ? token.Substring(0, 20) : token);
                    logger.LogInformation("Token ends with: ...{Last20}", token.Length > 20 ? token.Substring(token.Length - 20) : token);

                    try
                    {
                        var handler = new JwtSecurityTokenHandler();

                        // Check if token is readable
                        if (!handler.CanReadToken(token))
                        {
                            logger.LogError("Token cannot be read by JwtSecurityTokenHandler");
                            logger.LogDebug("Token value (first 100 chars): {TokenPrefix}",
                                token.Length > 100 ? token.Substring(0, 100) : token);
                        }
                        else
                        {
                            var jwtToken = handler.ReadJwtToken(token);

                            logger.LogInformation("=== JWT TOKEN DECODED ===");
                            logger.LogInformation("Token Subject (sub): {Subject}", jwtToken.Subject);
                            logger.LogInformation("Token Issuer: {Issuer}", jwtToken.Issuer);
                            logger.LogInformation("Token Audience: {Audience}", string.Join(", ", jwtToken.Audiences));
                            logger.LogInformation("Token Issued At: {IssuedAt:u}", jwtToken.IssuedAt);
                            logger.LogInformation("Token Expires: {Expires:u}", jwtToken.ValidTo);
                            logger.LogInformation("Server UTC Now: {Now:u}", DateTime.UtcNow);
                            logger.LogInformation("Time until expiration: {Minutes} minutes",
                                (jwtToken.ValidTo - DateTime.UtcNow).TotalMinutes);

                            // Check if token is expired
                            if (jwtToken.ValidTo < DateTime.UtcNow)
                            {
                                logger.LogWarning("TOKEN IS EXPIRED! Expired at: {ExpiredAt:u} ({MinutesAgo} minutes ago)",
                                    jwtToken.ValidTo, (DateTime.UtcNow - jwtToken.ValidTo).TotalMinutes);
                            }
                            else if (jwtToken.ValidTo.AddMinutes(-5) < DateTime.UtcNow) // Expiring soon
                            {
                                logger.LogWarning("Token expiring soon! Expires in: {Seconds} seconds",
                                    (jwtToken.ValidTo - DateTime.UtcNow).TotalSeconds);
                            }

                            // Log custom claims
                            logger.LogInformation("=== Token Claims ===");
                            foreach (var claim in jwtToken.Claims)
                            {
                                logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
                            }

                            // Check signature algorithm
                            if (jwtToken.Header.Alg != "HS256")
                            {
                                logger.LogWarning("Unexpected algorithm: {Algorithm}", jwtToken.Header.Alg);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "FAILED TO DECODE TOKEN");
                        logger.LogDebug("Problematic token: {TokenFirst200}...",
                            token.Length > 200 ? token.Substring(0, 200) : token);
                    }
                }
            }

            return Task.CompletedTask;
        },

        // Log authentication failures
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

            logger.LogError("=== JWT AUTHENTICATION FAILED ===");
            logger.LogError("Request: {Method} {Path}", context.Request.Method, context.Request.Path);
            logger.LogError("Exception Type: {ExceptionType}", context.Exception.GetType().Name);
            logger.LogError("Exception Message: {Message}", context.Exception.Message);

            // Check for specific failure types
            if (context.Exception is SecurityTokenExpiredException)
            {
                logger.LogError("TOKEN EXPIRED");

                // Log the expired token details
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring(7);
                    try
                    {
                        var handler = new JwtSecurityTokenHandler();
                        var jwt = handler.ReadJwtToken(token);
                        logger.LogError("Expired token was issued at: {IssuedAt:u}, expired at: {ExpiredAt:u}",
                            jwt.IssuedAt, jwt.ValidTo);
                    }
                    catch { }
                }
            }
            else if (context.Exception is SecurityTokenInvalidAudienceException)
            {
                logger.LogError("INVALID AUDIENCE");
                logger.LogError("Expected audience: {ExpectedAudience}", jwtSettings["Audience"]);
            }
            else if (context.Exception is SecurityTokenInvalidIssuerException)
            {
                logger.LogError("INVALID ISSUER");
                logger.LogError("Expected issuer: {ExpectedIssuer}", jwtSettings["Issuer"]);
            }
            else if (context.Exception is SecurityTokenInvalidSignatureException)
            {
                logger.LogError("INVALID SIGNATURE");
                logger.LogError("The token signature is invalid. This could be due to:");
                logger.LogError("1. Wrong signing key");
                logger.LogError("2. Token tampering");
                logger.LogError("3. Key mismatch between Portal and API");
            }
            else if (context.Exception.Message.Contains("not yet valid"))
            {
                logger.LogError("TOKEN NOT YET VALID (CLOCK SKEW)");
                logger.LogError("Server UTC: {UtcNow:u}", DateTime.UtcNow);
                logger.LogError("Server Local: {LocalNow:u}", DateTime.Now);
            }

            // Log full exception details for debugging
            logger.LogDebug(context.Exception, "Full authentication exception");

            return Task.CompletedTask;
        },

        // Log when challenge is triggered (401 response)
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

            logger.LogWarning("=== JWT CHALLENGE TRIGGERED (401) ===");
            logger.LogWarning("Request: {Method} {Path}", context.Request.Method, context.Request.Path);
            logger.LogWarning("Error: {Error}", context.Error);
            logger.LogWarning("Error Description: {ErrorDescription}", context.ErrorDescription);

            if (context.AuthenticateFailure != null)
            {
                logger.LogWarning("Authentication Failure: {FailureType} - {FailureMessage}",
                    context.AuthenticateFailure.GetType().Name,
                    context.AuthenticateFailure.Message);
            }
            else
            {
                logger.LogWarning("No AuthenticationFailure - This usually means:");
                logger.LogWarning("1. No Authorization header was sent");
                logger.LogWarning("2. Authorization header format is wrong (not 'Bearer token')");
                logger.LogWarning("3. No authentication scheme matched");

                // Verify the header one more time
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader))
                {
                    logger.LogError("CONFIRMED: No Authorization header in request");
                }
                else if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogError("CONFIRMED: Authorization header doesn't start with 'Bearer '");
                    logger.LogError("Header value: {Header}", authHeader);
                }
                else
                {
                    logger.LogError("CONFIRMED: Authorization header exists and starts with 'Bearer '");
                    logger.LogError("This suggests a middleware or configuration issue");
                }
            }

            // Log correlation ID for tracking
            logger.LogWarning("Correlation ID: {CorrelationId}", context.HttpContext.TraceIdentifier);

            return Task.CompletedTask;
        },

        // Log successful token validation
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

            var email = context.Principal?.FindFirst("email")?.Value
                        ?? context.Principal?.FindFirst(ClaimTypes.Email)?.Value
                        ?? "unknown";

            var userId = context.Principal?.FindFirst("sub")?.Value
                        ?? context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? "unknown";

            logger.LogInformation("=== JWT TOKEN VALIDATED SUCCESSFULLY ===");
            logger.LogInformation("User: {Email} (ID: {UserId})", email, userId);

            // Log all claims for debugging
            logger.LogDebug("=== User Claims ===");
            foreach (var claim in context.Principal.Claims)
            {
                logger.LogDebug("Claim: {Type} = {Value}", claim.Type, claim.Value);
            }

            return Task.CompletedTask;
        },

        // Log when authorization fails
        OnForbidden = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

            logger.LogWarning("=== JWT FORBIDDEN (403) ===");
            logger.LogWarning("Request: {Method} {Path}", context.Request.Method, context.Request.Path);
            logger.LogWarning("User: {User}", context.HttpContext.User?.Identity?.Name ?? "unknown");

            // Log user roles
            var roles = context.HttpContext.User?.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .ToList();

            logger.LogWarning("User roles: {Roles}", string.Join(", ", roles ?? new List<string>()));

            return Task.CompletedTask;
        }
    };
});





// Add custom user validator to check IsActive status

builder.Services.AddScoped<ActiveUserAttribute>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.Configure<EmailService.EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Configuration.AddEnvironmentVariables();



var app = builder.Build();

// Use the custom request logging middleware - ADD THIS BEFORE OTHER MIDDLEWARE
app.UseMiddleware<RequestLoggingMiddleware>();

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

// Add this BEFORE app.UseAuthentication()
app.Use(async (context, next) =>
{
    // Check for custom header and map it to Authorization header
    if (context.Request.Headers.TryGetValue("X-Auth-Token", out var customToken))
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Custom X-Auth-Token header found, mapping to Authorization header");

        // Only add if Authorization header is missing
        if (!context.Request.Headers.ContainsKey("Authorization"))
        {
            context.Request.Headers["Authorization"] = customToken.ToString();
            logger.LogInformation("Authorization header added from X-Auth-Token");
        }
        else
        {
            logger.LogWarning("Both X-Auth-Token and Authorization headers present");
        }
    }

    await next();
});

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Custom middleware for detailed request logging
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Log the incoming request
        _logger.LogInformation("=== INCOMING REQUEST ===");
        _logger.LogInformation("Method: {Method}", context.Request.Method);
        _logger.LogInformation("Path: {Path}", context.Request.Path);
        _logger.LogInformation("QueryString: {QueryString}", context.Request.QueryString);
        _logger.LogInformation("Content-Type: {ContentType}", context.Request.ContentType);
        _logger.LogInformation("Content-Length: {ContentLength}", context.Request.ContentLength);

        // Log all headers
        _logger.LogInformation("=== REQUEST HEADERS ===");
        foreach (var header in context.Request.Headers)
        {
            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(header.Value))
                {
                    _logger.LogInformation("Header: {Key} = {ValueFirst100}...",
                        header.Key,
                        header.Value.ToString().Length > 100 ? header.Value.ToString().Substring(0, 100) : header.Value);
                }
                else
                {
                    _logger.LogInformation("Header: {Key} = (empty)", header.Key);
                }
            }
            else
            {
                _logger.LogInformation("Header: {Key} = {Value}", header.Key, header.Value);
            }
        }

        // Log cookies (if any)
        _logger.LogInformation("=== REQUEST COOKIES ===");
        foreach (var cookie in context.Request.Cookies)
        {
            _logger.LogInformation("Cookie: {Key} = {ValueFirst50}...",
                cookie.Key,
                cookie.Value.Length > 50 ? cookie.Value.Substring(0, 50) : cookie.Value);
        }

        // Capture original response body for logging
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            // Log the response
            responseBody.Seek(0, SeekOrigin.Begin);
            var responseText = await new StreamReader(responseBody).ReadToEndAsync();
            responseBody.Seek(0, SeekOrigin.Begin);

            _logger.LogInformation("=== RESPONSE ===");
            _logger.LogInformation("Status Code: {StatusCode}", context.Response.StatusCode);
            _logger.LogInformation("Content-Type: {ContentType}", context.Response.ContentType);
            _logger.LogInformation("Response length: {Length} bytes", responseText.Length);

            if (context.Response.StatusCode >= 400)
            {
                _logger.LogWarning("Response body (first 500 chars): {ResponseBody}",
                    responseText.Length > 500 ? responseText.Substring(0, 500) : responseText);
            }

            await responseBody.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in request pipeline");
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}
