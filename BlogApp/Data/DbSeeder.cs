using BlogApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration config)
    {
        foreach (var role in new[] { AppRoles.SuperAdmin, AppRoles.Author, AppRoles.Reader })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

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
                // SuperAdmin ONLY — not Author/Reader (avoids showing as author in lists)
                await userManager.AddToRoleAsync(user, AppRoles.SuperAdmin);

                await userManager.AddClaimAsync(user, new System.Security.Claims.Claim(AppClaims.CanModerateAllComments, "true"));
                await userManager.AddClaimAsync(user, new System.Security.Claims.Claim(AppClaims.CanManageAllPosts, "true"));
                await userManager.AddClaimAsync(user, new System.Security.Claims.Claim(AppClaims.CanViewAllAnalytics, "true"));
            }
        }
        else
        {
            // Repair existing seed: SuperAdmin must not also be Author/Reader
            if (await userManager.IsInRoleAsync(existing, AppRoles.SuperAdmin))
            {
                if (await userManager.IsInRoleAsync(existing, AppRoles.Author))
                    await userManager.RemoveFromRoleAsync(existing, AppRoles.Author);
                if (await userManager.IsInRoleAsync(existing, AppRoles.Reader))
                    await userManager.RemoveFromRoleAsync(existing, AppRoles.Reader);

                // Ensure elevated claims exist
                var claims = await userManager.GetClaimsAsync(existing);
                async Task EnsureClaim(string type)
                {
                    if (claims.All(c => c.Type != type))
                        await userManager.AddClaimAsync(existing, new System.Security.Claims.Claim(type, "true"));
                }
                await EnsureClaim(AppClaims.CanModerateAllComments);
                await EnsureClaim(AppClaims.CanManageAllPosts);
                await EnsureClaim(AppClaims.CanViewAllAnalytics);
            }
        }

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
