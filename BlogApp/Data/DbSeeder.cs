using BlogApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

/// <summary>
/// Seeds roles, default SuperAdmin, and taxonomy. No demo posts/views.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration config)
    {
        // Roles
        foreach (var role in new[] { AppRoles.SuperAdmin, AppRoles.Author })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Default SuperAdmin from config (or sensible defaults)
        var adminUser = config["Admin:Username"] ?? "admin";
        var adminPass = config["Admin:Password"] ?? "ChangeMe123!";
        var adminEmail = config["Admin:Email"] ?? $"{adminUser}@localhost";

        var existing = await userManager.FindByNameAsync(adminUser);
        if (existing is null)
        {
            var user = new ApplicationUser
            {
                UserName = adminUser,
                Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = config["Seo:AuthorName"] ?? "مدیر کل",
                Bio = "Super administrator"
            };
            var result = await userManager.CreateAsync(user, adminPass);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, AppRoles.SuperAdmin);
                await userManager.AddToRoleAsync(user, AppRoles.Author);

                // SuperAdmin claims — full visibility
                await userManager.AddClaimAsync(user, new System.Security.Claims.Claim(AppClaims.CanModerateAllComments, "true"));
                await userManager.AddClaimAsync(user, new System.Security.Claims.Claim(AppClaims.CanManageAllPosts, "true"));
                await userManager.AddClaimAsync(user, new System.Security.Claims.Claim(AppClaims.CanViewAllAnalytics, "true"));
            }
        }

        // Categories
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Name = "دات‌نت", Slug = "dotnet" },
                new Category { Name = "معماری", Slug = "architecture" },
                new Category { Name = "یادداشت‌ها", Slug = "notes" }
            );
            await db.SaveChangesAsync();
        }
    }
}
