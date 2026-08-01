using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureAdminSearchAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS AdminSearchDocuments (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EntityType TEXT NOT NULL,
    EntityKey TEXT NOT NULL,
    Title TEXT NOT NULL,
    Subtitle TEXT NULL,
    BodyText TEXT NULL,
    Url TEXT NULL,
    Icon TEXT NULL,
    Status TEXT NULL,
    LanguageCode TEXT NULL,
    FacetsJson TEXT NULL,
    UpdatedAtUtc TEXT NULL,
    IndexedAtUtc TEXT NOT NULL,
    Boost INTEGER NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_AdminSearch_TypeKey ON AdminSearchDocuments(EntityType, EntityKey);
CREATE INDEX IF NOT EXISTS IX_AdminSearch_Type ON AdminSearchDocuments(EntityType);
");
    }
}
