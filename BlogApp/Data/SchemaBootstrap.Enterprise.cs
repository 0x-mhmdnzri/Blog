using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    private static async Task EnsureEnterpriseTablesAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Tenants" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Tenants" PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL DEFAULT 1,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Tenants_Code\" ON \"Tenants\" (\"Code\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Workspaces" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Workspaces" PRIMARY KEY AUTOINCREMENT,
                "TenantId" INTEGER NOT NULL,
                "Code" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "IsIsolated" INTEGER NOT NULL DEFAULT 1,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_Workspaces_Tenants" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "TenantDomains" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_TenantDomains" PRIMARY KEY AUTOINCREMENT,
                "TenantId" INTEGER NOT NULL,
                "Host" TEXT NOT NULL,
                "IsPrimary" INTEGER NOT NULL DEFAULT 0,
                "IsVerified" INTEGER NOT NULL DEFAULT 0,
                "VerificationToken" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_TenantDomains_Tenants" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_TenantDomains_Host\" ON \"TenantDomains\" (\"Host\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SsoProviderConfigs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SsoProviderConfigs" PRIMARY KEY AUTOINCREMENT,
                "TenantId" INTEGER NULL,
                "Protocol" TEXT NOT NULL,
                "DisplayName" TEXT NOT NULL,
                "Authority" TEXT NOT NULL,
                "ClientId" TEXT NOT NULL,
                "ClientSecret" TEXT NULL,
                "MetadataUrl" TEXT NULL,
                "IsEnabled" INTEGER NOT NULL DEFAULT 0,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ContentApprovalRequests" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ContentApprovalRequests" PRIMARY KEY AUTOINCREMENT,
                "PostId" INTEGER NOT NULL,
                "State" INTEGER NOT NULL,
                "SubmittedByUserId" TEXT NOT NULL,
                "ReviewerUserId" TEXT NULL,
                "Notes" TEXT NULL,
                "SubmittedAtUtc" TEXT NOT NULL,
                "ResolvedAtUtc" TEXT NULL,
                CONSTRAINT "FK_ContentApproval_Posts" FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ContentLifecycleRecords" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ContentLifecycleRecords" PRIMARY KEY AUTOINCREMENT,
                "PostId" INTEGER NOT NULL,
                "Stage" INTEGER NOT NULL,
                "ReviewDueAtUtc" TEXT NULL,
                "ArchiveAtUtc" TEXT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "UpdatedByUserId" TEXT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_ContentLifecycle_PostId\" ON \"ContentLifecycleRecords\" (\"PostId\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "LegalHolds" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_LegalHolds" PRIMARY KEY AUTOINCREMENT,
                "PostId" INTEGER NULL,
                "UserId" TEXT NULL,
                "Reason" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL DEFAULT 1,
                "CreatedAtUtc" TEXT NOT NULL,
                "CreatedByUserId" TEXT NOT NULL,
                "ReleasedAtUtc" TEXT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ConsentLogs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ConsentLogs" PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NULL,
                "Email" TEXT NOT NULL,
                "Purpose" TEXT NOT NULL,
                "Granted" INTEGER NOT NULL,
                "IpHash" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "DataExportRequests" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_DataExportRequests" PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NOT NULL,
                "RequestedAtUtc" TEXT NOT NULL,
                "CompletedAtUtc" TEXT NULL,
                "Status" TEXT NOT NULL,
                "FilePath" TEXT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "BackupRecords" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BackupRecords" PRIMARY KEY AUTOINCREMENT,
                "FileName" TEXT NOT NULL,
                "SizeBytes" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "CreatedByUserId" TEXT NOT NULL,
                "Kind" TEXT NOT NULL,
                "Notes" TEXT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "LocalizationEntries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_LocalizationEntries" PRIMARY KEY AUTOINCREMENT,
                "Key" TEXT NOT NULL,
                "LanguageCode" TEXT NOT NULL,
                "Group" TEXT NOT NULL,
                "Value" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "AssigneeUserId" TEXT NULL,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_LocalizationEntries_Key_Lang\" ON \"LocalizationEntries\" (\"Key\", \"LanguageCode\");");
    }
}
