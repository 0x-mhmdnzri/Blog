using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureAsync(ApplicationDbContext db, ILogger? logger = null)
    {
        if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync();

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

        await TryAddColumnAsync(db, "Posts", "IsFeatured", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "IsSticky", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "ScheduledPublishAtUtc", "TEXT NULL");
        await TryAddColumnAsync(db, "Posts", "ExpiresAtUtc", "TEXT NULL");
        await TryAddColumnAsync(db, "Posts", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "DeletedAtUtc", "TEXT NULL");
        await TryAddColumnAsync(db, "Posts", "ReadingTimeMinutes", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "IsPremium", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "IsSponsored", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "SponsoredLabel", "TEXT NULL");
        await TryAddColumnAsync(db, "Posts", "LikeCount", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "LanguageCode", "TEXT NOT NULL DEFAULT 'fa'");
        await TryAddColumnAsync(db, "Posts", "TranslationGroupId", "INTEGER NULL");
        await TryAddColumnAsync(db, "Posts", "TranslationStatus", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "CoverMediaAssetId", "INTEGER NULL");

        await TryAddColumnAsync(db, "AspNetUsers", "DisplayName", "TEXT NOT NULL DEFAULT ''");
        await TryAddColumnAsync(db, "AspNetUsers", "Bio", "TEXT NULL");
        await TryAddColumnAsync(db, "AspNetUsers", "ProfileImage", "BLOB NULL");
        await TryAddColumnAsync(db, "AspNetUsers", "ProfileImageContentType", "TEXT NULL");
        await TryAddColumnAsync(db, "AspNetUsers", "CreatedAtUtc", "TEXT NOT NULL DEFAULT '2020-01-01'");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PostLikes" (
                "PostId" INTEGER NOT NULL,
                "UserId" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "PK_PostLikes" PRIMARY KEY ("PostId", "UserId"),
                CONSTRAINT "FK_PostLikes_Posts_PostId"
                    FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PostLikes_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_PostLikes_UserId\" ON \"PostLikes\" (\"UserId\");");

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

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PostBookmarks" (
                "UserId" TEXT NOT NULL,
                "PostId" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "PK_PostBookmarks" PRIMARY KEY ("UserId", "PostId"),
                CONSTRAINT "FK_PostBookmarks_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PostBookmarks_Posts_PostId"
                    FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_PostBookmarks_UserId\" ON \"PostBookmarks\" (\"UserId\");");

        await TryAddColumnAsync(db, "Comments", "LikeCount", "INTEGER NOT NULL DEFAULT 0");
        await EnsureCommentColumnsAsync(db);
        await EnsurePostReviewColumnsAsync(db);
        await EnsureAuthorApplicationColumnsAsync(db);
        await EnsureAuthorProfileColumnsAsync(db);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CommentLikes" (
                "CommentId" INTEGER NOT NULL,
                "UserId" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "PK_CommentLikes" PRIMARY KEY ("CommentId", "UserId"),
                CONSTRAINT "FK_CommentLikes_Comments_CommentId"
                    FOREIGN KEY ("CommentId") REFERENCES "Comments" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_CommentLikes_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);

        await EnsureNotificationTablesAsync(db);
        await EnsureAnalyticsTablesAsync(db);
        await EnsureBotCrawlTablesAsync(db);
        await EnsureAdministrationTablesAsync(db);
        await EnsureI18nTablesAsync(db);
        await EnsureMonetizationTablesAsync(db);
        await EnsureSocialTablesAsync(db);
        await EnsurePerformanceTablesAsync(db);
        await EnsureSearchFtsAsync(db);
        await EnsureAdminSearchAsync(db);
        await EnsureApiTablesAsync(db);
        await EnsureNewsletterTablesAsync(db);
        await EnsureThemeTablesAsync(db);
        await EnsureEnterpriseTablesAsync(db);
        await EnsureFolderTablesAsync(db);
        // P4 — backlink leads + quarterly DA/DR snapshots
        await SchemaBootstrapAuthority.EnsureAuthorityTablesAsync(db);

        logger?.LogInformation("Schema bootstrap complete");
    }

    private static async Task TryAddColumnAsync(ApplicationDbContext db, string table, string column, string sqlType)
    {
        if (await ColumnExistsAsync(db, table, column))
            return;

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {sqlType};");
        }
        catch (Exception)
        {
        }
    }

    private static async Task<bool> ColumnExistsAsync(ApplicationDbContext db, string table, string column)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        if (cmd.Connection!.State != System.Data.ConnectionState.Open)
            await cmd.Connection.OpenAsync();

        cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
