using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AdminAnalyticsController
{
    /// <summary>DataTables JSON for posts with heatmap click counts.</summary>
    [HttpGet]
    public async Task<IActionResult> HeatmapsData()
    {
        var req = DataTablesRequest.From(Request);
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanViewAllAnalytics(User);

        var postsQ = _db.Posts.AsNoTracking().Where(p => !p.IsDeleted);
        if (!seeAll)
            postsQ = postsQ.Where(p => p.AuthorId == userId);

        var clickCounts = await _db.HeatmapClicks.AsNoTracking()
            .GroupBy(h => h.PostId)
            .Select(g => new { PostId = g.Key, Clicks = g.Count() })
            .ToListAsync();
        var clickMap = clickCounts.ToDictionary(x => x.PostId, x => x.Clicks);

        var posts = await postsQ
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                p.ViewCount,
                p.PublishedAtUtc,
                p.CreatedAtUtc
            })
            .ToListAsync();

        var rowsAll = posts.Select(p => new
        {
            p.Id,
            p.Title,
            p.Slug,
            p.ViewCount,
            Clicks = clickMap.GetValueOrDefault(p.Id, 0),
            Date = p.PublishedAtUtc ?? p.CreatedAtUtc
        }).ToList();

        var total = rowsAll.Count;

        if (!string.IsNullOrWhiteSpace(req.SearchValue))
        {
            var term = req.SearchValue.Trim();
            rowsAll = rowsAll.Where(p =>
                p.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || p.Slug.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var filtered = rowsAll.Count;

        rowsAll = (req.OrderColumn, req.Asc) switch
        {
            (1, true) => rowsAll.OrderBy(p => p.Title).ToList(),
            (1, false) => rowsAll.OrderByDescending(p => p.Title).ToList(),
            (2, true) => rowsAll.OrderBy(p => p.ViewCount).ToList(),
            (2, false) => rowsAll.OrderByDescending(p => p.ViewCount).ToList(),
            (3, true) => rowsAll.OrderBy(p => p.Clicks).ToList(),
            (3, false) => rowsAll.OrderByDescending(p => p.Clicks).ToList(),
            (4, true) => rowsAll.OrderBy(p => p.Date).ToList(),
            (4, false) => rowsAll.OrderByDescending(p => p.Date).ToList(),
            _ => rowsAll.OrderByDescending(p => p.Clicks).ThenByDescending(p => p.ViewCount).ToList()
        };

        var page = rowsAll.Skip(req.Start).Take(req.Length).ToList();
        var openLabel = System.Net.WebUtility.HtmlEncode(_t["ana.heatmap_open"]);

        var rows = page.Select((p, i) => new object[]
        {
            req.Start + i + 1,
            System.Net.WebUtility.HtmlEncode(p.Title),
            p.ViewCount,
            p.Clicks,
            PersianDate.Date(p.Date),
            "<a class=\"icon-btn\" href=\"/AdminAnalytics/Heatmap/" + p.Id + "\">" + openLabel + "</a>"
        }).ToList();

        return Json(DataTablesResponse.Ok(req.Draw, total, filtered, rows));
    }

    [HttpGet]
    public async Task<IActionResult> Heatmap(int id, int range = 30)
    {
        if (range is not (0 or 7 or 30 or 90)) range = 30;

        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanViewAllAnalytics(User);

        var post = await _db.Posts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (post is null) return NotFound();
        if (!seeAll && post.AuthorId != userId) return Forbid();

        var q = _db.HeatmapClicks.AsNoTracking().Where(h => h.PostId == id);
        if (range > 0)
        {
            var start = DateTime.UtcNow.Date.AddDays(-range);
            q = q.Where(h => h.ClickedAtUtc >= start);
        }

        var raw = await q.Select(h => new { h.X, h.Y }).ToListAsync();
        var points = raw
            .GroupBy(h => (h.X, h.Y))
            .Select(g => new HeatmapPoint { X = g.Key.X, Y = g.Key.Y, Count = g.Count() })
            .OrderByDescending(p => p.Count)
            .Take(500)
            .ToList();

        ViewData["Title"] = _t["ana.heatmap"] + " — " + post.Title;
        return View(new HeatmapDetailViewModel
        {
            PostId = post.Id,
            Title = post.Title,
            Slug = post.Slug,
            RangeDays = range,
            TotalClicks = raw.Count,
            UniqueCells = points.Count,
            Points = points
        });
    }
}
