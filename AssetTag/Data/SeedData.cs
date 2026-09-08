// Data/SeedData.cs
using Shared.Models;
using Shared.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssetTag.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(
            IServiceProvider serviceProvider,
            IWebHostEnvironment env,
            IConfiguration config)
        {
            using var scope = serviceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

            // Always ensure built-in roles exist (safe to re-run)
            foreach (var role in RoleNames.BuiltIn)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                    logger.LogInformation("Created role {Role}", role);
                }
            }

            // Only create the initial admin if no users exist
            if (await userManager.Users.AnyAsync())
                return;

            var adminEmail = config["InitialAdmin:Email"];
            var adminPassword = config["InitialAdmin:Password"];
            var adminUsername = config["InitialAdmin:Username"] ?? "admin";

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                if (!env.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        "InitialAdmin:Email and InitialAdmin:Password must be configured to seed the first admin user.");
                }

                // Development-only bootstrap so an empty local DB remains usable.
                // Password is never logged.
                adminEmail = string.IsNullOrWhiteSpace(adminEmail) ? "admin@assettag.local" : adminEmail;
                adminPassword = string.IsNullOrWhiteSpace(adminPassword) ? "ChangeMe_DevOnly_123!" : adminPassword;
                logger.LogWarning(
                    "InitialAdmin credentials missing; seeding Development-only admin for {Email}. " +
                    "Set InitialAdmin:Email and InitialAdmin:Password (user secrets or env) before non-dev deploys.",
                    adminEmail);
            }

            var adminUser = new ApplicationUser
            {
                UserName = adminUsername,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "System",
                Surname = "Administrator",
                IsActive = true,
                DateCreated = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, RoleNames.Admin);
                logger.LogInformation("Default Admin created successfully for {Email}", adminEmail);
            }
            else
            {
                throw new Exception("Failed to create initial admin: " + string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            await context.SaveChangesAsync();
        }
    }
}
