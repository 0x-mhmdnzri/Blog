using BlogApp.Services;
using BlogApp.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BlogApp.Controllers;

/// <summary>Public beacons for duration + heatmap (rate-limited).</summary>
[AllowAnonymous]
[EnableRateLimiting("global")]
public class AnalyticsController : Controller
{
    private readonly IAnalyticsTracker _tracker;

    public AnalyticsController(IAnalyticsTracker tracker) => _tracker = tracker;

    public class DurationDto
    {
        public int PostId { get; set; }
        public int Seconds { get; set; }
    }

    public class HeatmapDto
    {
        public int PostId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Duration([FromBody] DurationDto dto)
    {
        if (dto is null || dto.PostId <= 0) return BadRequest();
        var hash = VisitorIdentity.ComputeHash(HttpContext);
        await _tracker.TrackReadingDurationAsync(dto.PostId, dto.Seconds, hash);
        return Ok();
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Heatmap([FromBody] HeatmapDto dto)
    {
        if (dto is null || dto.PostId <= 0) return BadRequest();
        await _tracker.TrackHeatmapClickAsync(dto.PostId, dto.X, dto.Y);
        return Ok();
    }
}
