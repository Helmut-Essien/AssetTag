// Data/SeedData.cs
using Shared.Models;
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

            // Only seed if no users exist (safe for production)
            if (await userManager.Users.AnyAsync())
                return;

            // Create Roles
            string[] roles = { "Admin", "Manager", "User" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Create Default Admin from config or fallback
            var adminEmail = config["InitialAdmin:Email"] ?? "admin@assettag.com";
            var adminPassword = config["InitialAdmin:Password"] ?? "Admin@12345";
            var adminUsername = config["InitialAdmin:Username"] ?? "admin";

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
                await userManager.AddToRoleAsync(adminUser, "Admin");
                Console.WriteLine("Default Admin created successfully!");
                Console.WriteLine($"Email: {adminEmail}");
                Console.WriteLine($"Password: {adminPassword}");
            }
            else
            {
                throw new Exception("Failed to create initial admin: " + string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            await context.SaveChangesAsync();
        }
    }
}