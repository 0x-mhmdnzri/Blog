using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureAuthorProfileColumnsAsync(ApplicationDbContext db)
    {
        await TryAddColumnAsync(db, "AspNetUsers", "Gender", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "AspNetUsers", "Twitter", "TEXT NULL");
        await TryAddColumnAsync(db, "AspNetUsers", "LinkedIn", "TEXT NULL");
        await TryAddColumnAsync(db, "AspNetUsers", "Telegram", "TEXT NULL");
        await TryAddColumnAsync(db, "AspNetUsers", "Phone", "TEXT NULL");
        await TryAddColumnAsync(db, "AspNetUsers", "Website", "TEXT NULL");
        await TryAddColumnAsync(db, "AspNetUsers", "GitHub", "TEXT NULL");
        await TryAddColumnAsync(db, "AspNetUsers", "Instagram", "TEXT NULL");
    }
}
