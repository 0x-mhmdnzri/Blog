using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureAnalyticsTablesAsync(ApplicationDbContext db)
    {
        await TryAddColumnAsync(db, "PostViews", "SessionKey", "TEXT NULL");
        await TryAddColumnAsync(db, "PostViews", "DeviceType", "TEXT NULL");
        await TryAddColumnAsync(db, "PostViews", "Browser", "TEXT NULL");
        await TryAddColumnAsync(db, "PostViews", "Os", "TEXT NULL");
        await TryAddColumnAsync(db, "PostViews", "TrafficSource", "TEXT NULL");
        await TryAddColumnAsync(db, "PostViews", "ReferrerHost", "TEXT NULL");
        await TryAddColumnAsync(db, "PostViews", "CountryCode", "TEXT NULL");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AnalyticsSessions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AnalyticsSessions" PRIMARY KEY AUTOINCREMENT,
                "SessionKey" TEXT NOT NULL,
                "VisitorHash" TEXT NOT NULL,
                "StartedAtUtc" TEXT NOT NULL,
                "LastSeenAtUtc" TEXT NOT NULL,
                "PageViewCount" INTEGER NOT NULL,
                "DeviceType" TEXT NULL,
                "Browser" TEXT NULL,
                "Os" TEXT NULL,
                "CountryCode" TEXT NULL,
                "TrafficSource" TEXT NULL,
                "ReferrerHost" TEXT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_AnalyticsSessions_SessionKey\" ON \"AnalyticsSessions\" (\"SessionKey\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SearchQueryLogs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SearchQueryLogs" PRIMARY KEY AUTOINCREMENT,
                "Query" TEXT NOT NULL,
                "ResultCount" INTEGER NOT NULL,
                "SearchedAtUtc" TEXT NOT NULL,
                "VisitorHash" TEXT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ReadingDurationLogs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ReadingDurationLogs" PRIMARY KEY AUTOINCREMENT,
                "PostId" INTEGER NOT NULL,
                "DurationSeconds" INTEGER NOT NULL,
                "LoggedAtUtc" TEXT NOT NULL,
                "VisitorHash" TEXT NULL,
                CONSTRAINT "FK_ReadingDurationLogs_Posts_PostId"
                    FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "HeatmapClicks" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_HeatmapClicks" PRIMARY KEY AUTOINCREMENT,
                "PostId" INTEGER NOT NULL,
                "X" INTEGER NOT NULL,
                "Y" INTEGER NOT NULL,
                "ClickedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_HeatmapClicks_Posts_PostId"
                    FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE
            );
            """);
    }
}
