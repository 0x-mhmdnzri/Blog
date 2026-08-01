using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureNewsletterTablesAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "NewsletterSubscribers" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_NewsletterSubscribers" PRIMARY KEY AUTOINCREMENT,
                "Email" TEXT NOT NULL,
                "Name" TEXT NULL,
                "UserId" TEXT NULL,
                "Status" INTEGER NOT NULL,
                "LanguageCode" TEXT NOT NULL,
                "SegmentTags" TEXT NULL,
                "ConfirmToken" TEXT NOT NULL,
                "UnsubscribeToken" TEXT NOT NULL,
                "SubscribedAtUtc" TEXT NOT NULL,
                "ConfirmedAtUtc" TEXT NULL,
                "UnsubscribedAtUtc" TEXT NULL,
                "Source" TEXT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_NewsletterSubscribers_Email\" ON \"NewsletterSubscribers\" (\"Email\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_NewsletterSubscribers_Status\" ON \"NewsletterSubscribers\" (\"Status\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "NewsletterSegments" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_NewsletterSegments" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Description" TEXT NULL,
                "LanguageCode" TEXT NULL,
                "RequiredTag" TEXT NULL,
                "ConfirmedOnly" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "NewsletterCampaigns" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_NewsletterCampaigns" PRIMARY KEY AUTOINCREMENT,
                "Subject" TEXT NOT NULL,
                "BodyHtml" TEXT NOT NULL,
                "BodyText" TEXT NULL,
                "SegmentId" INTEGER NULL,
                "LanguageFilter" TEXT NULL,
                "TagFilter" TEXT NULL,
                "Status" INTEGER NOT NULL,
                "ScheduledAtUtc" TEXT NULL,
                "SentAtUtc" TEXT NULL,
                "RecipientCount" INTEGER NOT NULL,
                "SentCount" INTEGER NOT NULL,
                "FailCount" INTEGER NOT NULL,
                "CreatedByUserId" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_NewsletterCampaigns_Segments"
                    FOREIGN KEY ("SegmentId") REFERENCES "NewsletterSegments" ("Id") ON DELETE SET NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_NewsletterCampaigns_Status\" ON \"NewsletterCampaigns\" (\"Status\");");
    }
}
