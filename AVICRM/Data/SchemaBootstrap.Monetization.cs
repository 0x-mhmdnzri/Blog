using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureMonetizationTablesAsync(ApplicationDbContext db)
    {
        await TryAddColumnAsync(db, "Posts", "IsPremium", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "IsSponsored", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(db, "Posts", "SponsoredLabel", "TEXT NULL");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SubscriptionPlans" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SubscriptionPlans" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Code" TEXT NOT NULL,
                "Description" TEXT NULL,
                "Price" TEXT NOT NULL,
                "Currency" TEXT NOT NULL,
                "DurationDays" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_SubscriptionPlans_Code\" ON \"SubscriptionPlans\" (\"Code\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "UserSubscriptions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_UserSubscriptions" PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NOT NULL,
                "PlanId" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "StartedAtUtc" TEXT NOT NULL,
                "EndsAtUtc" TEXT NULL,
                "PaymentReference" TEXT NULL,
                "Notes" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_UserSubscriptions_Plans" FOREIGN KEY ("PlanId") REFERENCES "SubscriptionPlans" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_UserSubscriptions_UserId\" ON \"UserSubscriptions\" (\"UserId\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Donations" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Donations" PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NULL,
                "DonorName" TEXT NULL,
                "DonorEmail" TEXT NULL,
                "Amount" TEXT NOT NULL,
                "Currency" TEXT NOT NULL,
                "Message" TEXT NULL,
                "IsAnonymous" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "PaymentReference" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "ConfirmedAtUtc" TEXT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Advertisements" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Advertisements" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Placement" INTEGER NOT NULL,
                "HtmlContent" TEXT NOT NULL,
                "TargetUrl" TEXT NULL,
                "IsActive" INTEGER NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "StartsAtUtc" TEXT NULL,
                "EndsAtUtc" TEXT NULL,
                "ImpressionCount" INTEGER NOT NULL,
                "ClickCount" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AffiliateLinks" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AffiliateLinks" PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "DestinationUrl" TEXT NOT NULL,
                "Network" TEXT NULL,
                "IsActive" INTEGER NOT NULL,
                "ClickCount" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "LastClickAtUtc" TEXT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_AffiliateLinks_Code\" ON \"AffiliateLinks\" (\"Code\");");
    }
}
