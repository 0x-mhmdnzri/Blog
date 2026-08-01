using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class PostsController
{
    /// <summary>
    /// FEATURES.md: Post → newsletter one-click send.
    /// Creates a NewsletterCampaign from the published post and queues send.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    public async Task<IActionResult> SendToNewsletter(int id, [FromServices] INewsletterService nl)
    {
        var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (post is null) return NotFound();
        if (!post.IsPublished)
        {
            TempData["Error"] = "Publish the post before sending to the newsletter.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var userId = AuthorAccess.UserId(User)!;
        if (!User.IsInRole(AppRoles.SuperAdmin) && post.AuthorId != userId)
            return Forbid();

        var summary = System.Net.WebUtility.HtmlEncode(post.Summary ?? "");
        var slug = System.Net.WebUtility.HtmlEncode(post.Slug);
        var bodyHtml =
            "<p>" + summary + "</p>\n" +
            "<p><a href=\"/post/" + slug + "\">Read full post →</a></p>";

        var campaign = new NewsletterCampaign
        {
            Subject = post.Title,
            BodyHtml = bodyHtml,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            Status = NewsletterCampaignStatus.Scheduled,
            ScheduledAtUtc = DateTime.UtcNow
        };
        _db.NewsletterCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        try
        {
            await nl.SendCampaignAsync(campaign.Id);
            TempData["Success"] = "Newsletter campaign queued from this post.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Newsletter send from post failed PostId={Id}", id);
            TempData["Error"] = "Campaign created but send failed — check newsletter status.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }
}
