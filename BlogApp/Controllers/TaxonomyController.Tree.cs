using BlogApp.Models;
using BlogApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class TaxonomyController
{
    private async Task<List<CategoryTreeItem>> BuildCategoryTreeAsync()
    {
        var all = await _db.Categories
            .Select(c => new { c.Id, c.Name, c.Slug, c.Description, c.ParentId, c.DisplayOrder, PostCount = c.Posts.Count })
            .ToListAsync();

        var byParent = all.GroupBy(c => c.ParentId).ToDictionary(g => g.Key ?? -1, g => g.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToList());

        List<CategoryTreeItem> Walk(int? parentId, int depth)
        {
            var key = parentId ?? -1;
            if (!byParent.TryGetValue(key, out var children)) return new();
            var list = new List<CategoryTreeItem>();
            foreach (var c in children)
            {
                list.Add(new CategoryTreeItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Slug = c.Slug,
                    Description = c.Description,
                    ParentId = c.ParentId,
                    Depth = depth,
                    PostCount = c.PostCount
                });
                list.AddRange(Walk(c.Id, depth + 1));
            }
            return list;
        }

        return Walk(null, 0);
    }
}
