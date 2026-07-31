using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsurePerformanceTablesAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "BackgroundJobs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BackgroundJobs" PRIMARY KEY AUTOINCREMENT,
                "Type" TEXT NOT NULL,
                "Payload" TEXT NULL,
                "Status" INTEGER NOT NULL,
                "Attempts" INTEGER NOT NULL,
                "MaxAttempts" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "AvailableAtUtc" TEXT NULL,
                "StartedAtUtc" TEXT NULL,
                "CompletedAtUtc" TEXT NULL,
                "LastError" TEXT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_BackgroundJobs_Status_Available\" ON \"BackgroundJobs\" (\"Status\", \"AvailableAtUtc\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SearchIndexEntries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SearchIndexEntries" PRIMARY KEY AUTOINCREMENT,
                "PostId" INTEGER NOT NULL,
                "LanguageCode" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Slug" TEXT NOT NULL,
                "Summary" TEXT NULL,
                "BodyText" TEXT NULL,
                "TagsCsv" TEXT NULL,
                "CategoryName" TEXT NULL,
                "IsPublished" INTEGER NOT NULL,
                "PublishedAtUtc" TEXT NULL,
                "IndexedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_SearchIndexEntries_Posts_PostId"
                    FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_SearchIndexEntries_PostId\" ON \"SearchIndexEntries\" (\"PostId\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_SearchIndexEntries_Lang_Pub\" ON \"SearchIndexEntries\" (\"LanguageCode\", \"IsPublished\");");

        await TryAddColumnAsync(db, "MediaAssets", "Width", "INTEGER NULL");
        await TryAddColumnAsync(db, "MediaAssets", "Height", "INTEGER NULL");
        await TryAddColumnAsync(db, "MediaAssets", "Version", "INTEGER NOT NULL DEFAULT 1");
        await TryAddColumnAsync(db, "MediaAssets", "OptimizedAtUtc", "TEXT NULL");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "MediaVariants" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MediaVariants" PRIMARY KEY AUTOINCREMENT,
                "MediaAssetId" INTEGER NOT NULL,
                "Width" INTEGER NOT NULL,
                "Height" INTEGER NOT NULL,
                "ContentType" TEXT NOT NULL,
                "SizeBytes" INTEGER NOT NULL,
                "Content" BLOB NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_MediaVariants_MediaAssets_MediaAssetId"
                    FOREIGN KEY ("MediaAssetId") REFERENCES "MediaAssets" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_MediaVariants_MediaAssetId_Width\" ON \"MediaVariants\" (\"MediaAssetId\", \"Width\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "MediaVersions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MediaVersions" PRIMARY KEY AUTOINCREMENT,
                "MediaAssetId" INTEGER NOT NULL,
                "VersionNumber" INTEGER NOT NULL,
                "ContentType" TEXT NOT NULL,
                "SizeBytes" INTEGER NOT NULL,
                "Content" BLOB NOT NULL,
                "Width" INTEGER NULL,
                "Height" INTEGER NULL,
                "Note" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_MediaVersions_MediaAssets_MediaAssetId"
                    FOREIGN KEY ("MediaAssetId") REFERENCES "MediaAssets" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_MediaVersions_MediaAssetId\" ON \"MediaVersions\" (\"MediaAssetId\");");
    }
}
