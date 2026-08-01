using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

public static partial class SchemaBootstrap
{
    /// <summary>
    /// SQLite FTS5 virtual table for full-text search + author columns on SearchIndexEntries.
    /// Rows are kept in sync by SearchIndexService.
    /// </summary>
    public static async Task EnsureSearchFtsAsync(ApplicationDbContext db)
    {
        await TryAddColumnAsync(db, "SearchIndexEntries", "AuthorUserId", "TEXT NULL");
        await TryAddColumnAsync(db, "SearchIndexEntries", "AuthorName", "TEXT NULL");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE VIRTUAL TABLE IF NOT EXISTS "PostsFts" USING fts5(
                Title,
                Summary,
                BodyText,
                TagsCsv,
                CategoryName,
                AuthorName,
                LanguageCode,
                PostId UNINDEXED,
                tokenize = 'unicode61 remove_diacritics 2'
            );
            """);
    }
}
