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
    }
}
