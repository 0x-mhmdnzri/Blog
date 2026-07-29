using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

/// <summary>
/// EnsureCreated only builds the schema when the DB file is new.
/// This helper patches an existing SQLite file with tables/columns added later.
/// </summary>
public static class SchemaBootstrap
{
    public static async Task EnsureAsync(ApplicationDbContext db)
    {
        // New tables (SEO redirects + broken-link scan)
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RedirectRules" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RedirectRules" PRIMARY KEY AUTOINCREMENT,
                "FromPath" TEXT NOT NULL,
                "ToUrl" TEXT NOT NULL,
                "StatusCode" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "Notes" TEXT NULL,
                "HitCount" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "LastHitAtUtc" TEXT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_RedirectRules_FromPath\" ON \"RedirectRules\" (\"FromPath\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_RedirectRules_IsActive\" ON \"RedirectRules\" (\"IsActive\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "BrokenLinkReports" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BrokenLinkReports" PRIMARY KEY AUTOINCREMENT,
                "PostId" INTEGER NOT NULL,
                "PostTitle" TEXT NOT NULL,
                "PostSlug" TEXT NOT NULL,
                "Url" TEXT NOT NULL,
                "NormalizedPath" TEXT NULL,
                "DetectedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_BrokenLinkReports_Posts_PostId"
                    FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_BrokenLinkReports_PostId\" ON \"BrokenLinkReports\" (\"PostId\");");

        // CMS columns on Posts (ignore if already present)
        await TryAddColumnAsync(db, "Posts", "IsFeatured", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "IsSticky", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "ScheduledPublishAtUtc", "TEXT NULL");
        await TryAddColumnAsync(db, "Posts", "ExpiresAtUtc", "TEXT NULL");
        await TryAddColumnAsync(db, "Posts", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "DeletedAtUtc", "TEXT NULL");
        await TryAddColumnAsync(db, "Posts", "ReadingTimeMinutes", "INTEGER NOT NULL DEFAULT 0");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PostRevisions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PostRevisions" PRIMARY KEY AUTOINCREMENT,
                "PostId" INTEGER NOT NULL,
                "Title" TEXT NOT NULL,
                "Summary" TEXT NULL,
                "ContentMarkdown" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "Note" TEXT NULL,
                "CreatedByUserId" TEXT NULL,
                CONSTRAINT "FK_PostRevisions_Posts_PostId"
                    FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_PostRevisions_PostId\" ON \"PostRevisions\" (\"PostId\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_PostRevisions_CreatedAtUtc\" ON \"PostRevisions\" (\"CreatedAtUtc\");");
    }

    private static async Task TryAddColumnAsync(ApplicationDbContext db, string table, string column, string sqlType)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {sqlType};");
        }
        catch
        {
            // Column already exists — SQLite throws; safe to ignore.
        }
    }
}
