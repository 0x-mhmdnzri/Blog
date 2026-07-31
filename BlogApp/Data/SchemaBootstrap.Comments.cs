using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureCommentColumnsAsync(ApplicationDbContext db)
    {
        await TryAddColumnAsync(db, "Comments", "ParentId", "INTEGER NULL");
        await TryAddColumnAsync(db, "Comments", "UserId", "TEXT NULL");
        await TryAddColumnAsync(db, "Comments", "AuthorEmail", "TEXT NULL");
        await TryAddColumnAsync(db, "Comments", "IsGuest", "INTEGER NOT NULL DEFAULT 1");
        await TryAddColumnAsync(db, "Comments", "IsPinned", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Comments", "PinnedAtUtc", "TEXT NULL");
        await TryAddColumnAsync(db, "Comments", "EditedAtUtc", "TEXT NULL");
        await TryAddColumnAsync(db, "Comments", "EditCount", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Comments", "SpamScore", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Comments", "SpamReasons", "TEXT NULL");
        await TryAddColumnAsync(db, "Comments", "IpHash", "TEXT NULL");

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS \"IX_Comments_ParentId\" ON \"Comments\" (\"ParentId\");");
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS \"IX_Comments_IsPinned\" ON \"Comments\" (\"IsPinned\");");
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS \"IX_Comments_UserId\" ON \"Comments\" (\"UserId\");");
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS \"IX_Comments_Status\" ON \"Comments\" (\"Status\");");
        }
        catch { /* ignore */ }
    }
}
