using EventsApp.Common;
using EventsApp.Models;
using Microsoft.AspNetCore.Identity;

namespace EventsApp.Infrastructure
{
    public static class AdminSeeder
    {
        // Change these before deploying to production
        private const string AdminEmail = "admin@grooveon.com";
        private const string AdminUserName = "admin@grooveon.com";
        private const string AdminPassword = "admin";

        // Legacy email used in earlier builds — migrated to AdminEmail on startup
        private const string LegacyAdminEmail = "admin@groooveon.com";

        public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var admin = await userManager.FindByEmailAsync(AdminEmail)
                        ?? await userManager.FindByEmailAsync(LegacyAdminEmail);

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = AdminUserName,
                    Email = AdminEmail,
                    EmailConfirmed = true,
                };

                var create = await userManager.CreateAsync(admin, AdminPassword);
                if (!create.Succeeded) return;
            }
            else
            {
                admin.UserName = AdminUserName;
                admin.NormalizedUserName = AdminUserName.ToUpperInvariant();
                admin.Email = AdminEmail;
                admin.NormalizedEmail = AdminEmail.ToUpperInvariant();
                admin.EmailConfirmed = true;
                await userManager.UpdateAsync(admin);

                var token = await userManager.GeneratePasswordResetTokenAsync(admin);
                await userManager.ResetPasswordAsync(admin, token, AdminPassword);
            }

            if (!await userManager.IsInRoleAsync(admin, GlobalConstants.Roles.Admin))
            {
                await userManager.AddToRoleAsync(admin, GlobalConstants.Roles.Admin);
            }
        }
    }
}
