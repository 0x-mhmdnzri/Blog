using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureBotCrawlTablesAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "BotCrawlHits" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BotCrawlHits" PRIMARY KEY AUTOINCREMENT,
                "HitAtUtc" TEXT NOT NULL,
                "BotFamily" TEXT NOT NULL,
                "BotKind" TEXT NOT NULL,
                "UserAgent" TEXT NULL,
                "Method" TEXT NOT NULL,
                "Path" TEXT NOT NULL,
                "Query" TEXT NULL,
                "StatusCode" INTEGER NOT NULL,
                "ElapsedMs" INTEGER NOT NULL,
                "IpHash" TEXT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_BotCrawlHits_HitAtUtc\" ON \"BotCrawlHits\" (\"HitAtUtc\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_BotCrawlHits_BotFamily_HitAtUtc\" ON \"BotCrawlHits\" (\"BotFamily\", \"HitAtUtc\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_BotCrawlHits_StatusCode\" ON \"BotCrawlHits\" (\"StatusCode\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_BotCrawlHits_BotKind\" ON \"BotCrawlHits\" (\"BotKind\");");
    }
}
