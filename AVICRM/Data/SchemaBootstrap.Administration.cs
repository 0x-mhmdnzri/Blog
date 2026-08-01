using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureAdministrationTablesAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SiteSettings" (
                "Key" TEXT NOT NULL CONSTRAINT "PK_SiteSettings" PRIMARY KEY,
                "Value" TEXT NULL,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "FeatureFlags" (
                "Key" TEXT NOT NULL CONSTRAINT "PK_FeatureFlags" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "Description" TEXT NULL,
                "IsEnabled" INTEGER NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AuditLogs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AuditLogs" PRIMARY KEY AUTOINCREMENT,
                "ActorUserId" TEXT NULL,
                "ActorUserName" TEXT NULL,
                "Action" TEXT NOT NULL,
                "EntityType" TEXT NULL,
                "EntityId" TEXT NULL,
                "Details" TEXT NULL,
                "IpAddress" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_AuditLogs_CreatedAtUtc\" ON \"AuditLogs\" (\"CreatedAtUtc\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_AuditLogs_Action\" ON \"AuditLogs\" (\"Action\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ContentReports" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ContentReports" PRIMARY KEY AUTOINCREMENT,
                "TargetType" INTEGER NOT NULL,
                "TargetId" INTEGER NOT NULL,
                "TargetTitle" TEXT NULL,
                "Reason" TEXT NOT NULL,
                "Details" TEXT NULL,
                "ReporterUserId" TEXT NULL,
                "ReporterName" TEXT NULL,
                "Status" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "ResolvedAtUtc" TEXT NULL,
                "ResolvedByUserId" TEXT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_ContentReports_Status\" ON \"ContentReports\" (\"Status\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_ContentReports_CreatedAtUtc\" ON \"ContentReports\" (\"CreatedAtUtc\");");
    }
}
