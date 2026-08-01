using AVICRM.Models;
using AVICRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Controllers;

public partial class PostsController
{
    /// <summary>
    /// FEATURES.md: Post → newsletter one-click send.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    public async Task<IActionResult> SendToNewsletter(int id, [FromServices] INewsletterService nl)
    {
        var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (post is null) return NotFound();

        var userId = AuthorAccess.UserId(User)!;
        if (!User.IsInRole(AppRoles.SuperAdmin) && post.AuthorId != userId)
            return Forbid();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var (ok, msg, _) = await nl.PublishPostAsCampaignAsync(id, userId, baseUrl, sendNow: true);

        if (ok) TempData["Success"] = msg;
        else TempData["Error"] = msg;

        return RedirectToAction(nameof(Edit), new { id });
    }
}
