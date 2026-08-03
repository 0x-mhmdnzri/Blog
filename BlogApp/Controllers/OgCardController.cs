using BlogApp.Data;
using BlogApp.Services.Seo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[AllowAnonymous]
[Route("og")]
public sealed class OgCardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPostOgCardService _cards;

    public OgCardController(ApplicationDbContext db, IPostOgCardService cards)
    {
        _db = db;
        _cards = cards;
    }

    [HttpGet("post/{id:int}.png")]
    [HttpGet("post/{id:int}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "v" })]
    public async Task<IActionResult> PostCard(int id, CancellationToken ct)
    {
        var post = await _db.Posts.AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted && p.IsPublished, ct);
        if (post is null) return NotFound();

        var png = await _cards.GetOrCreatePngAsync(post, ct);
        if (png is null || png.Length == 0) return NotFound();

        Response.Headers.CacheControl = "public, max-age=86400";
        return File(png, "image/png");
    }
}
