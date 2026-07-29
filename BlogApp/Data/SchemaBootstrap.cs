using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static class SchemaBootstrap
{
    public static async Task EnsureAsync(ApplicationDbContext db)
    {
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

        // Nested categories
        await TryAddColumnAsync(db, "Categories", "ParentId", "INTEGER NULL");
        await TryAddColumnAsync(db, "Categories", "Description", "TEXT NULL");
        await TryAddColumnAsync(db, "Categories", "DisplayOrder", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Tags", "Description", "TEXT NULL");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PostSeries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PostSeries" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Slug" TEXT NOT NULL,
                "Description" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_PostSeries_Slug\" ON \"PostSeries\" (\"Slug\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SeriesPosts" (
                "SeriesId" INTEGER NOT NULL,
                "PostId" INTEGER NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                CONSTRAINT "PK_SeriesPosts" PRIMARY KEY ("SeriesId", "PostId"),
                CONSTRAINT "FK_SeriesPosts_PostSeries_SeriesId" FOREIGN KEY ("SeriesId") REFERENCES "PostSeries" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SeriesPosts_Posts_PostId" FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "TopicCollections" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_TopicCollections" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Slug" TEXT NOT NULL,
                "Description" TEXT NULL,
                "IsPublished" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_TopicCollections_Slug\" ON \"TopicCollections\" (\"Slug\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "TopicCollectionItems" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_TopicCollectionItems" PRIMARY KEY AUTOINCREMENT,
                "TopicCollectionId" INTEGER NOT NULL,
                "CategoryId" INTEGER NULL,
                "TagId" INTEGER NULL,
                "SortOrder" INTEGER NOT NULL,
                CONSTRAINT "FK_TopicCollectionItems_TopicCollections_TopicCollectionId"
                    FOREIGN KEY ("TopicCollectionId") REFERENCES "TopicCollections" ("Id") ON DELETE CASCADE
            );
            """);
    }

    private static async Task TryAddColumnAsync(ApplicationDbContext db, string table, string column, string sqlType)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {sqlType};");
        }
        catch { /* already exists */ }
    }
}
