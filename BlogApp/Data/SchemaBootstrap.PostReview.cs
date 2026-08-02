using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsurePostReviewColumnsAsync(ApplicationDbContext db)
    {
        await TryAddColumnAsync(db, "Posts", "ReviewStatus", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "ReviewNote", "TEXT NULL");
    }
}
