using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureNotificationsAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AppNotifications" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AppNotifications" PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NOT NULL,
                "Kind" INTEGER NOT NULL,
                "Title" TEXT NOT NULL,
                "Body" TEXT NULL,
                "LinkUrl" TEXT NULL,
                "IsRead" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "CampaignId" INTEGER NULL,
                CONSTRAINT "FK_AppNotifications_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_AppNotifications_UserId_IsRead\" ON \"AppNotifications\" (\"UserId\", \"IsRead\");");
        await TryAddColumnAsync(db, "AppNotifications", "CampaignId", "INTEGER NULL");
        await TryAddColumnAsync(db, "AppNotifications", "IsStarred", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "AppNotifications", "IsArchived", "INTEGER NOT NULL DEFAULT 0");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "NotificationPreferences" (
                "UserId" TEXT NOT NULL CONSTRAINT "PK_NotificationPreferences" PRIMARY KEY,
                "EmailEnabled" INTEGER NOT NULL,
                "InAppEnabled" INTEGER NOT NULL,
                "PushEnabled" INTEGER NOT NULL,
                "SmsEnabled" INTEGER NOT NULL,
                "NotifyNewComment" INTEGER NOT NULL,
                "NotifyNewFollower" INTEGER NOT NULL,
                "WeeklyDigest" INTEGER NOT NULL,
                "NotifyNewPostFromFollowed" INTEGER NOT NULL DEFAULT 1,
                "PhoneE164" TEXT NULL,
                CONSTRAINT "FK_NotificationPreferences_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);
        await TryAddColumnAsync(db, "NotificationPreferences", "NotifyNewPostFromFollowed", "INTEGER NOT NULL DEFAULT 1");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PushSubscriptions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PushSubscriptions" PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NOT NULL,
                "Endpoint" TEXT NOT NULL,
                "P256dh" TEXT NOT NULL,
                "Auth" TEXT NOT NULL,
                "UserAgent" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "LastUsedAtUtc" TEXT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_PushSubscriptions_Endpoint\" ON \"PushSubscriptions\" (\"Endpoint\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_PushSubscriptions_UserId\" ON \"PushSubscriptions\" (\"UserId\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "WebhookDeliveries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WebhookDeliveries" PRIMARY KEY AUTOINCREMENT,
                "SubscriptionId" INTEGER NOT NULL,
                "EventType" TEXT NOT NULL,
                "TargetUrl" TEXT NOT NULL,
                "HttpStatus" INTEGER NULL,
                "Success" INTEGER NOT NULL,
                "Error" TEXT NULL,
                "Attempt" INTEGER NOT NULL DEFAULT 1,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_WebhookDeliveries_Sub\" ON \"WebhookDeliveries\" (\"SubscriptionId\", \"CreatedAtUtc\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AuthorFollows" (
                "FollowerUserId" TEXT NOT NULL,
                "AuthorUserId" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "PK_AuthorFollows" PRIMARY KEY ("FollowerUserId", "AuthorUserId"),
                CONSTRAINT "FK_AuthorFollows_Follower" FOREIGN KEY ("FollowerUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_AuthorFollows_Author" FOREIGN KEY ("AuthorUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "OutboundMessages" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_OutboundMessages" PRIMARY KEY AUTOINCREMENT,
                "Channel" TEXT NOT NULL,
                "To" TEXT NOT NULL,
                "Subject" TEXT NULL,
                "Body" TEXT NOT NULL,
                "IsHtml" INTEGER NOT NULL,
                "IsSent" INTEGER NOT NULL,
                "SentAtUtc" TEXT NULL,
                "Error" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UserId" TEXT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "NotificationCampaigns" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_NotificationCampaigns" PRIMARY KEY AUTOINCREMENT,
                "Title" TEXT NOT NULL,
                "Body" TEXT NULL,
                "LinkUrl" TEXT NULL,
                "Kind" INTEGER NOT NULL,
                "Audience" INTEGER NOT NULL,
                "TargetUserId" TEXT NULL,
                "AuthorUserId" TEXT NULL,
                "CategoryId" INTEGER NULL,
                "TargetUserIdsCsv" TEXT NULL,
                "CreatedByUserId" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "ScheduledAtUtc" TEXT NULL,
                "IsSent" INTEGER NOT NULL,
                "SentAtUtc" TEXT NULL,
                "RecipientCount" INTEGER NOT NULL
            );
            """);
    }
}
