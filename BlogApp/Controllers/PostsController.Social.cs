using BlogApp.Models;
using BlogApp.Services;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class PostsController
{
    private async Task LoadSocialContextAsync(Post post)
    {
        var uid = AuthorAccess.UserId(User);

        ViewBag.SocialPostId = post.Id;
        ViewBag.SocialSlug = post.Slug;
        ViewBag.SocialTitle = post.Title;
        ViewBag.SocialLikeCount = post.LikeCount;
        ViewBag.SocialAuthorId = post.AuthorId;
        ViewBag.SocialCategoryId = post.CategoryId;

        ViewBag.SocialLiked = uid != null
            && await _db.PostLikes.AnyAsync(l => l.PostId == post.Id && l.UserId == uid);

        if (uid != null)
        {
            ViewBag.SocialMyReaction = await _db.PostReactions.AsNoTracking()
                .Where(r => r.PostId == post.Id && r.UserId == uid)
                .Select(r => (ReactionKind?)r.Kind)
                .FirstOrDefaultAsync();

            ViewBag.SocialFollowingAuthor = await _db.AuthorFollows.AsNoTracking()
                .AnyAsync(f => f.FollowerUserId == uid && f.AuthorUserId == post.AuthorId);

            if (post.CategoryId is int catId)
            {
                ViewBag.SocialFollowingCategory = await _db.CategoryFollows.AsNoTracking()
                    .AnyAsync(f => f.UserId == uid && f.CategoryId == catId);
            }
        }

        var counts = await _db.PostReactions.AsNoTracking()
            .Where(r => r.PostId == post.Id)
            .GroupBy(r => r.Kind)
            .Select(g => new { g.Key, C = g.Count() })
            .ToListAsync();

        ViewBag.SocialReactionCounts = counts.ToDictionary(x => x.Key, x => x.C);
    }
}
