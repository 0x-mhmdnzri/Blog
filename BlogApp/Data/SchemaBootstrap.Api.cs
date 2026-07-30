using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureApiTablesAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ApiKeys" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ApiKeys" PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "KeyPrefix" TEXT NOT NULL,
                "KeyHash" TEXT NOT NULL,
                "Scopes" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "IsBanned" INTEGER NOT NULL,
                "BanReason" TEXT NULL,
                "BannedAtUtc" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "LastUsedAtUtc" TEXT NULL,
                "ExpiresAtUtc" TEXT NULL,
                "RequestCount" INTEGER NOT NULL,
                "AbuseStrikeCount" INTEGER NOT NULL,
                "LastAbuseAtUtc" TEXT NULL,
                CONSTRAINT "FK_ApiKeys_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_ApiKeys_KeyHash\" ON \"ApiKeys\" (\"KeyHash\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_ApiKeys_UserId\" ON \"ApiKeys\" (\"UserId\");");

        // Approval workflow columns (idempotent for existing DBs)
        await TryAlterAsync(db, "ALTER TABLE \"ApiKeys\" ADD COLUMN \"ApprovalStatus\" INTEGER NOT NULL DEFAULT 0;");
        await TryAlterAsync(db, "ALTER TABLE \"ApiKeys\" ADD COLUMN \"ApprovedAtUtc\" TEXT NULL;");
        await TryAlterAsync(db, "ALTER TABLE \"ApiKeys\" ADD COLUMN \"ApprovedByUserId\" TEXT NULL;");
        await TryAlterAsync(db, "ALTER TABLE \"ApiKeys\" ADD COLUMN \"RejectionReason\" TEXT NULL;");
        // Existing keys created before approval feature → treat as approved
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "ApiKeys"
            SET "ApprovalStatus" = 1,
                "ApprovedAtUtc" = COALESCE("ApprovedAtUtc", "CreatedAtUtc")
            WHERE "ApprovalStatus" = 0
              AND "IsActive" = 1
              AND "RequestCount" > 0;
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "WebhookSubscriptions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WebhookSubscriptions" PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NOT NULL,
                "ApiKeyId" INTEGER NULL,
                "TargetUrl" TEXT NOT NULL,
                "Secret" TEXT NOT NULL,
                "Events" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "LastDeliveryAtUtc" TEXT NULL,
                "FailureCount" INTEGER NOT NULL,
                CONSTRAINT "FK_WebhookSubscriptions_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_WebhookSubscriptions_UserId\" ON \"WebhookSubscriptions\" (\"UserId\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ApiRequestLogs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ApiRequestLogs" PRIMARY KEY AUTOINCREMENT,
                "ApiKeyId" INTEGER NULL,
                "UserId" TEXT NULL,
                "UserName" TEXT NULL,
                "KeyPrefix" TEXT NULL,
                "Method" TEXT NOT NULL,
                "Path" TEXT NOT NULL,
                "Query" TEXT NULL,
                "StatusCode" INTEGER NOT NULL,
                "DurationMs" INTEGER NOT NULL,
                "IpAddress" TEXT NULL,
                "UserAgent" TEXT NULL,
                "IsError" INTEGER NOT NULL,
                "IsRateLimited" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_ApiRequestLogs_CreatedAtUtc\" ON \"ApiRequestLogs\" (\"CreatedAtUtc\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_ApiRequestLogs_UserId\" ON \"ApiRequestLogs\" (\"UserId\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_ApiRequestLogs_ApiKeyId\" ON \"ApiRequestLogs\" (\"ApiKeyId\");");
    }

    private static async Task TryAlterAsync(ApplicationDbContext db, string sql)
    {
        try { await db.Database.ExecuteSqlRawAsync(sql); }
        catch { /* column may already exist */ }
    }
}
