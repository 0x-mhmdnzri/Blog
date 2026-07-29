using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

/// <summary>
/// Seeds only real, non-fake scaffolding — nothing that would show up as noise in the
/// analytics dashboard. No demo posts, no demo comments, no demo views. If you want a
/// realistic-looking dashboard, write real posts and generate real views by visiting them.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Models.Category { Name = "دات‌نت", Slug = "dotnet" },
                new Models.Category { Name = "معماری", Slug = "architecture" },
                new Models.Category { Name = "یادداشت‌ها", Slug = "notes" }
            );
            await db.SaveChangesAsync();
        }
    }
}
