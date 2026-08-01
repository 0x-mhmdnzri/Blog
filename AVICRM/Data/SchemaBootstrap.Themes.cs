using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureThemeTablesAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CustomThemes" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_CustomThemes" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Description" TEXT NULL,
                "OwnerUserId" TEXT NULL,
                "Status" INTEGER NOT NULL,
                "RejectionReason" TEXT NULL,
                "IsSystem" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "Bg" TEXT NOT NULL,
                "Surface" TEXT NOT NULL,
                "Surface2" TEXT NOT NULL,
                "Border" TEXT NOT NULL,
                "Text" TEXT NOT NULL,
                "TextMuted" TEXT NOT NULL,
                "Accent" TEXT NOT NULL,
                "Danger" TEXT NOT NULL,
                "Success" TEXT NOT NULL,
                "Mode" TEXT NOT NULL,
                "ContrastTextOnBg" REAL NOT NULL,
                "ContrastMutedOnBg" REAL NOT NULL,
                "ContrastAccentOnBg" REAL NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "ReviewedAtUtc" TEXT NULL,
                "ReviewedByUserId" TEXT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_CustomThemes_Status\" ON \"CustomThemes\" (\"Status\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_CustomThemes_IsActive\" ON \"CustomThemes\" (\"IsActive\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_CustomThemes_OwnerUserId\" ON \"CustomThemes\" (\"OwnerUserId\");");
    }
}
