using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

public static partial class SchemaBootstrap
{
    private static async Task EnsureEnterpriseTablesAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Organizations" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Organizations" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Slug" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
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

        await EnsureNavMenuAsync(db);
    }
}
