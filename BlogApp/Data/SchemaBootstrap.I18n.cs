using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureI18nTablesAsync(ApplicationDbContext db)
    {
        await TryAddColumnAsync(db, "Posts", "LanguageCode", "TEXT NOT NULL DEFAULT 'fa'");
        await TryAddColumnAsync(db, "Posts", "TranslationGroupId", "INTEGER NULL");
        await TryAddColumnAsync(db, "Posts", "TranslationStatus", "INTEGER NOT NULL DEFAULT 0");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Posts_LanguageCode\" ON \"Posts\" (\"LanguageCode\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Posts_TranslationGroupId\" ON \"Posts\" (\"TranslationGroupId\");");
        // Composite uniqueness: same slug may exist in different languages
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Posts_LanguageCode_Slug\" ON \"Posts\" (\"LanguageCode\", \"Slug\");");

        // Backfill: existing posts become Persian originals in their own translation group
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE "Posts"
                SET "LanguageCode" = COALESCE(NULLIF("LanguageCode", ''), 'fa'),
                    "TranslationStatus" = COALESCE("TranslationStatus", 0),
                    "TranslationGroupId" = COALESCE("TranslationGroupId", "Id")
                WHERE "TranslationGroupId" IS NULL OR "LanguageCode" IS NULL OR "LanguageCode" = '';
                """);
        }
        catch
        {
            // column may not exist on very old DBs until TryAddColumn ran
        }
    }
}
