using AVICRM.Models;
using AVICRM.Services;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

public static partial class SchemaBootstrap
{
    public static async Task EnsureNavMenuAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "NavMenuItems" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_NavMenuItems" PRIMARY KEY AUTOINCREMENT,
                "Key" TEXT NOT NULL,
                "LabelKey" TEXT NOT NULL,
                "ParentId" INTEGER NULL,
                "Controller" TEXT NULL,
                "Action" TEXT NULL,
                "IconPath" TEXT NULL,
                "SortOrder" INTEGER NOT NULL DEFAULT 0,
                "IsSection" INTEGER NOT NULL DEFAULT 0,
                "SuperAdminOnly" INTEGER NOT NULL DEFAULT 0,
                "StaffOnly" INTEGER NOT NULL DEFAULT 0,
                "DemoTag" INTEGER NOT NULL DEFAULT 0,
                "IsEnabled" INTEGER NOT NULL DEFAULT 1,
                "UpdatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_NavMenuItems_Parent" FOREIGN KEY ("ParentId") REFERENCES "NavMenuItems" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_NavMenuItems_Key\" ON \"NavMenuItems\" (\"Key\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_NavMenuItems_ParentId\" ON \"NavMenuItems\" (\"ParentId\");");

        await SeedNavMenuFromCatalogAsync(db);
    }

    /// <summary>Upsert hierarchical menu from AdminNavCatalog (FEATURES.md tree).</summary>
    public static async Task SeedNavMenuFromCatalogAsync(ApplicationDbContext db)
    {
        var existing = await db.NavMenuItems.AsNoTracking().Select(x => x.Key).ToListAsync();
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sort = 0;

        async Task Walk(AdminNavItem item, int? parentId)
        {
            sort++;
            if (!existingSet.Contains(item.Key))
            {
                var row = new NavMenuItem
                {
                    Key = item.Key,
                    LabelKey = item.LabelKey,
                    ParentId = parentId,
                    Controller = string.IsNullOrEmpty(item.Controller) ? null : item.Controller,
                    Action = string.IsNullOrEmpty(item.Action) ? null : item.Action,
                    IconPath = item.Icon.Length > 400 ? item.Icon[..400] : item.Icon,
                    SortOrder = sort,
                    IsSection = item.IsSection,
                    SuperAdminOnly = item.SuperAdminOnly,
                    StaffOnly = item.StaffOnly,
                    DemoTag = item.DemoTag,
                    IsEnabled = true,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                db.NavMenuItems.Add(row);
                await db.SaveChangesAsync();
                existingSet.Add(item.Key);
                parentId = row.Id;
            }
            else
            {
                var row = await db.NavMenuItems.FirstAsync(x => x.Key == item.Key);
                row.LabelKey = item.LabelKey;
                row.ParentId = parentId;
                row.Controller = string.IsNullOrEmpty(item.Controller) ? null : item.Controller;
                row.Action = string.IsNullOrEmpty(item.Action) ? null : item.Action;
                row.SortOrder = sort;
                row.IsSection = item.IsSection;
                row.SuperAdminOnly = item.SuperAdminOnly;
                row.StaffOnly = item.StaffOnly;
                row.DemoTag = item.DemoTag;
                row.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
                parentId = row.Id;
            }

            if (item.Children is { Length: > 0 })
            {
                foreach (var child in item.Children)
                    await Walk(child, parentId);
            }
        }

        foreach (var root in AdminNavCatalog.All)
            await Walk(root, null);
    }
}
