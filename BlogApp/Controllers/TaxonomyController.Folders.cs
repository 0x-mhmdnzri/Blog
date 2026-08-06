using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class TaxonomyController
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFolder(string name, string? description, string? color, int? parentId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return RedirectToAction(nameof(Index));

        var userId = AuthorAccess.UserId(User)!;
        var slug = await UniqueFolderSlugAsync(SlugHelper.Slugify(name));
        var tint = NormalizeFolderColor(color);
        _db.PostFolders.Add(new PostFolder
        {
            Name = name.Trim(),
            Slug = slug,
            Description = description?.Trim(),
            Color = tint,
            ParentId = parentId,
            OwnerUserId = userId,
            DisplayOrder = await _db.PostFolders.CountAsync(f => f.OwnerUserId == userId),
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["TaxonomyMsg"] = _t["tax.msg_folder_added"];
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFolder(int id)
    {
        var folder = await _db.PostFolders.AsTracking()
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.Id == id);
        if (folder is null) return NotFound();
        if (!CanManageFolder(folder)) return Forbid();
        if (folder.Children.Any())
        {
            TempData["TaxonomyErr"] = _t["tax.msg_folder_has_children"];
            return RedirectToAction(nameof(Index));
        }

        var items = await _db.PostFolderItems.AsTracking().Where(i => i.FolderId == id).ToListAsync();
        _db.PostFolderItems.RemoveRange(items);
        _db.PostFolders.Remove(folder);
        await _db.SaveChangesAsync();
        TempData["TaxonomyMsg"] = _t["tax.msg_folder_deleted"];
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Folder(int id, string? q = null, string? sort = null, int? categoryId = null, int? tagId = null)
    {
        var folder = await _db.PostFolders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (folder is null) return NotFound();
        if (!CanManageFolder(folder)) return Forbid();

        ViewData["Title"] = folder.Name;

        var query = _db.PostFolderItems.AsNoTracking()
            .Where(i => i.FolderId == id)
            .Select(i => i.Post)
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => p.Title.Contains(term) || (p.Summary != null && p.Summary.Contains(term)));
        }
        if (categoryId is int cid && cid > 0)
            query = query.Where(p => p.CategoryId == cid);
        if (tagId is int tid && tid > 0)
            query = query.Where(p => p.PostTags.Any(pt => pt.TagId == tid));

        query = (sort ?? "updated") switch
        {
            "title" => query.OrderBy(p => p.Title),
            "published" => query.OrderByDescending(p => p.PublishedAtUtc),
            "oldest" => query.OrderBy(p => p.UpdatedAtUtc),
            _ => query.OrderByDescending(p => p.UpdatedAtUtc)
        };

        var posts = await query
            .Take(200)
            .Select(p => new FolderPostItem
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                LanguageCode = p.LanguageCode,
                IsPublished = p.IsPublished,
                PublishedAtUtc = p.PublishedAtUtc,
                UpdatedAtUtc = p.UpdatedAtUtc,
                CategoryName = p.Category != null ? p.Category.Name : null,
                CategoryId = p.CategoryId,
                TagNames = p.PostTags.Select(pt => pt.Tag.Name).ToList()
            })
            .ToListAsync();

        var userId = AuthorAccess.UserId(User)!;
        var isSuper = AuthorAccess.IsSuperAdmin(User);
        var postScope = _db.Posts.AsNoTracking().Where(p => !p.IsDeleted);
        if (!isSuper) postScope = postScope.Where(p => p.AuthorId == userId);

        var cats = await _db.Categories.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new CategoryOption { Id = c.Id, Name = c.Name }).ToListAsync();
        var tags = await _db.Tags.AsNoTracking().OrderBy(t => t.Name)
            .Select(t => new TagAdminItem { Id = t.Id, Name = t.Name, Slug = t.Slug }).ToListAsync();

        var vm = new FolderDetailViewModel
        {
            Id = folder.Id,
            Name = folder.Name,
            Slug = folder.Slug,
            Description = folder.Description,
            Color = folder.Color,
            Search = q,
            Sort = sort ?? "updated",
            CategoryId = categoryId,
            TagId = tagId,
            Posts = posts,
            Categories = cats,
            Tags = tags
        };
        ViewBag.AvailablePosts = await postScope
            .Where(p => !_db.PostFolderItems.Any(i => i.FolderId == id && i.PostId == p.Id))
            .OrderByDescending(p => p.UpdatedAtUtc)
            .Select(p => new { p.Id, p.Title })
            .Take(80)
            .ToListAsync();
        return View("~/Views/Admin/Folder.cshtml", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPostToFolder(int folderId, int postId)
    {
        var folder = await _db.PostFolders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == folderId);
        if (folder is null) return NotFound();
        if (!CanManageFolder(folder)) return Forbid();

        if (!await _db.PostFolderItems.AnyAsync(i => i.FolderId == folderId && i.PostId == postId))
        {
            var max = await _db.PostFolderItems.Where(i => i.FolderId == folderId).MaxAsync(i => (int?)i.SortOrder) ?? 0;
            _db.PostFolderItems.Add(new PostFolderItem
            {
                FolderId = folderId,
                PostId = postId,
                SortOrder = max + 1,
                AddedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        TempData["TaxonomyMsg"] = _t["tax.msg_post_added_folder"];
        return RedirectToAction(nameof(Folder), new { id = folderId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePostFromFolder(int folderId, int postId)
    {
        var folder = await _db.PostFolders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == folderId);
        if (folder is null) return NotFound();
        if (!CanManageFolder(folder)) return Forbid();

        var row = await _db.PostFolderItems.AsTracking()
            .FirstOrDefaultAsync(i => i.FolderId == folderId && i.PostId == postId);
        if (row is not null)
        {
            _db.PostFolderItems.Remove(row);
            await _db.SaveChangesAsync();
        }
        TempData["TaxonomyMsg"] = _t["tax.msg_post_removed_folder"];
        return RedirectToAction(nameof(Folder), new { id = folderId });
    }

    private bool CanManageFolder(PostFolder folder)
    {
        if (AuthorAccess.IsSuperAdmin(User)) return true;
        var uid = AuthorAccess.UserId(User);
        return uid is not null && folder.OwnerUserId == uid;
    }

    private static string NormalizeFolderColor(string? color)
    {
        var c = (color ?? "blue").Trim().ToLowerInvariant();
        return c is "blue" or "yellow" or "red" or "green" or "purple" or "gray" or "orange" ? c : "blue";
    }

    private async Task<string> UniqueFolderSlugAsync(string baseSlug)
    {
        var slug = baseSlug; var i = 2;
        while (await _db.PostFolders.AnyAsync(f => f.Slug == slug))
            slug = $"{baseSlug}-{i++}";
        return slug;
    }
}
