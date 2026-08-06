using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureFolderTablesAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PostFolders" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Slug" TEXT NOT NULL,
                "Description" TEXT NULL,
                "Color" TEXT NOT NULL DEFAULT 'blue',
                "ParentId" INTEGER NULL,
                "OwnerUserId" TEXT NOT NULL,
                "DisplayOrder" INTEGER NOT NULL DEFAULT 0,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_PostFolders_Parent" FOREIGN KEY ("ParentId") REFERENCES "PostFolders" ("Id") ON DELETE RESTRICT
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_PostFolders_Slug\" ON \"PostFolders\" (\"Slug\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_PostFolders_OwnerUserId\" ON \"PostFolders\" (\"OwnerUserId\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PostFolderItems" (
                "FolderId" INTEGER NOT NULL,
                "PostId" INTEGER NOT NULL,
                "SortOrder" INTEGER NOT NULL DEFAULT 0,
                "AddedAtUtc" TEXT NOT NULL,
                CONSTRAINT "PK_PostFolderItems" PRIMARY KEY ("FolderId", "PostId"),
                CONSTRAINT "FK_PostFolderItems_Folder" FOREIGN KEY ("FolderId") REFERENCES "PostFolders" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PostFolderItems_Post" FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_PostFolderItems_PostId\" ON \"PostFolderItems\" (\"PostId\");");
    }
}
