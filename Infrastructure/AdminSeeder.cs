using EventsApp.Common;
using EventsApp.Models;
using Microsoft.AspNetCore.Identity;

namespace EventsApp.Infrastructure
{
    public static class AdminSeeder
    {
        // Change these before deploying to production
        private const string AdminEmail = "admin@groooveon.com";
        private const string AdminUserName = "admin";
        private const string AdminPassword = "Admin123!";

        public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var existing = await userManager.FindByEmailAsync(AdminEmail);
            if (existing != null) return;

            var admin = new ApplicationUser
            {
                UserName = AdminUserName,
                Email = AdminEmail,
                EmailConfirmed = true,
            };

            var result = await userManager.CreateAsync(admin, AdminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, GlobalConstants.Roles.Admin);
            }
        }
    }
}
