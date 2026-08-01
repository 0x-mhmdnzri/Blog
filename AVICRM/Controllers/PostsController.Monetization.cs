using AVICRM.Models;
using AVICRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AVICRM.Controllers;

public partial class PostsController
{
    /// <summary>
    /// For premium posts, non-members only see a teaser (first ~3 paragraphs of markdown).
    /// Authors / SuperAdmin / active members see full content.
    /// </summary>
    private async Task ApplyPremiumGateAsync(Post post)
    {
        ViewBag.IsPremiumPost = post.IsPremium;
        ViewBag.IsSponsoredPost = post.IsSponsored;
        ViewBag.SponsoredLabel = string.IsNullOrWhiteSpace(post.SponsoredLabel)
            ? null
            : post.SponsoredLabel;

        if (!post.IsPremium)
        {
            ViewBag.PremiumLocked = false;
            return;
        }

        if (AuthorAccess.OwnsPost(User, post) || AuthorAccess.IsSuperAdmin(User))
        {
            ViewBag.PremiumLocked = false;
            return;
        }

        var membership = HttpContext.RequestServices.GetRequiredService<IMembershipService>();
        var has = await membership.HasActiveMembershipAsync(AuthorAccess.UserId(User));
        if (has)
        {
            ViewBag.PremiumLocked = false;
            return;
        }

        ViewBag.PremiumLocked = true;
        ViewBag.RenderedHtml = _markdown.RenderToHtmlWithToc(BuildTeaser(post.ContentMarkdown), true);
    }

    private static string BuildTeaser(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var kept = new List<string>();
        var blocks = 0;
        foreach (var line in lines)
        {
            kept.Add(line);
            if (string.IsNullOrWhiteSpace(line))
            {
                blocks++;
                if (blocks >= 3) break;
            }
        }
        if (kept.Count == lines.Length) return markdown;
        return string.Join('\n', kept).TrimEnd() + "\n\n…";
    }
}
