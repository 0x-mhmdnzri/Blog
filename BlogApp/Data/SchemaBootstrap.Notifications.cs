using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureNotificationTablesAsync(ApplicationDbContext db)
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
                CONSTRAINT "FK_AppNotifications_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_AppNotifications_UserId_IsRead\" ON \"AppNotifications\" (\"UserId\", \"IsRead\");");

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
                "PhoneE164" TEXT NULL,
                CONSTRAINT "FK_NotificationPreferences_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);

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
    }
}
