using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Name = ".NET", Slug = "dotnet" },
                new Category { Name = "Architecture", Slug = "architecture" },
                new Category { Name = "Notes", Slug = "notes" }
            );
        }

        if (!await db.Posts.AnyAsync())
        {
            var dotnet = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "dotnet");

            db.Posts.Add(new Post
            {
                Title = "Welcome to the blog",
                Slug = "welcome-to-the-blog",
                Summary = "How this blog works: everything — text, images, video, code — lives in one database.",
                Category = dotnet,
                IsPublished = true,
                PublishedAtUtc = DateTime.UtcNow,
                ContentMarkdown =
"""
# Welcome

This is a **README-style** post editor. Write plain Markdown and it renders exactly
like a GitHub README — headings, lists, tables, block quotes, and fenced code:

```csharp
public record Post(string Title, string Slug, string ContentMarkdown);
```

> Every image, video, and file you drop into a post is uploaded straight into the
> database as bytes — nothing touches the disk. Delete the post, and its media goes
> with it automatically.

- No artificial length limit on post content
- Syntax-highlighted code blocks in the dark theme
- Drop in `{{video:ID}}` to embed an uploaded video inline

Happy writing.
"""
            });
        }

        await db.SaveChangesAsync();
    }
}
