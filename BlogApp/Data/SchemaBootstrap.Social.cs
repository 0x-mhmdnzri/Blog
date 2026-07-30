using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureSocialTablesAsync(ApplicationDbContext db)
    {
        await TryAddColumnAsync(db, "Posts", "LikeCount", "INTEGER NOT NULL DEFAULT 0");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PostLikes" (
                "PostId" INTEGER NOT NULL,
                "UserId" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "PK_PostLikes" PRIMARY KEY ("PostId", "UserId"),
                CONSTRAINT "FK_PostLikes_Posts_PostId"
                    FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PostLikes_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PostReactions" (
                "PostId" INTEGER NOT NULL,
                "UserId" TEXT NOT NULL,
                "Kind" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "PK_PostReactions" PRIMARY KEY ("PostId", "UserId"),
                CONSTRAINT "FK_PostReactions_Posts_PostId"
                    FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PostReactions_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_PostReactions_PostId\" ON \"PostReactions\" (\"PostId\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CategoryFollows" (
                "CategoryId" INTEGER NOT NULL,
                "UserId" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "PK_CategoryFollows" PRIMARY KEY ("CategoryId", "UserId"),
                CONSTRAINT "FK_CategoryFollows_Categories_CategoryId"
                    FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_CategoryFollows_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "UserActivities" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_UserActivities" PRIMARY KEY AUTOINCREMENT,
                "ActorUserId" TEXT NOT NULL,
                "Kind" INTEGER NOT NULL,
                "PostId" INTEGER NULL,
                "CategoryId" INTEGER NULL,
                "TargetUserId" TEXT NULL,
                "Title" TEXT NULL,
                "LinkUrl" TEXT NULL,
                "Meta" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_UserActivities_ActorUserId\" ON \"UserActivities\" (\"ActorUserId\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_UserActivities_CreatedAtUtc\" ON \"UserActivities\" (\"CreatedAtUtc\");");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "UserMentions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_UserMentions" PRIMARY KEY AUTOINCREMENT,
                "MentionedUserId" TEXT NOT NULL,
                "ActorUserId" TEXT NOT NULL,
                "CommentId" INTEGER NULL,
                "PostId" INTEGER NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_UserMentions_MentionedUserId\" ON \"UserMentions\" (\"MentionedUserId\");");
    }
}
