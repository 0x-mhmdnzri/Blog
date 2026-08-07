using BlogApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static class SchemaBootstrapAuthority
{
    public static async Task EnsureAuthorityTablesAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "BacklinkLeads" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BacklinkLeads" PRIMARY KEY AUTOINCREMENT,
                "TargetSite" TEXT NOT NULL,
                "TargetUrl" TEXT NULL,
                "OurUrl" TEXT NULL,
                "Contact" TEXT NULL,
                "Status" TEXT NOT NULL DEFAULT 'prospect',
                "Source" TEXT NULL,
                "DomainRating" INTEGER NULL,
                "Notes" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "AcquiredAtUtc" TEXT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_BacklinkLeads_Status" ON "BacklinkLeads" ("Status");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AuthoritySnapshots" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AuthoritySnapshots" PRIMARY KEY AUTOINCREMENT,
                "Period" TEXT NOT NULL,
                "MeasuredAtUtc" TEXT NOT NULL,
                "Provider" TEXT NOT NULL DEFAULT 'Ahrefs',
                "DomainRating" INTEGER NULL,
                "DomainAuthority" INTEGER NULL,
                "TrustFlow" INTEGER NULL,
                "CitationFlow" INTEGER NULL,
                "ReferringDomains" INTEGER NULL,
                "OrganicKeywords" INTEGER NULL,
                "Notes" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_AuthoritySnapshots_Period" ON "AuthoritySnapshots" ("Period");
            """);
    }
}
