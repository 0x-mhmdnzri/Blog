using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureAuthorApplicationColumnsAsync(ApplicationDbContext db)
    {
        await TryAddColumnAsync(db, "AspNetUsers", "AuthorApplicationStatus", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "AspNetUsers", "AuthorApplicationMessage", "TEXT NULL");
        await TryAddColumnAsync(db, "AspNetUsers", "AuthorAppliedAtUtc", "TEXT NULL");
        await TryAddColumnAsync(db, "AspNetUsers", "AuthorReviewNote", "TEXT NULL");
    }
}
