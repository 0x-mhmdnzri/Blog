using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services.Analytics;

public interface IAnalyticsTracker
{
    Task TrackPostViewAsync(HttpContext http, Post post, CancellationToken ct = default);
    Task TrackSearchAsync(HttpContext http, string query, int resultCount, CancellationToken ct = default);
    Task TrackReadingDurationAsync(int postId, int seconds, string? visitorHash, CancellationToken ct = default);
    Task TrackHeatmapClickAsync(int postId, int x, int y, CancellationToken ct = default);
}

public sealed class AnalyticsTracker : IAnalyticsTracker
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AnalyticsTracker> _logger;

    public AnalyticsTracker(ApplicationDbContext db, ILogger<AnalyticsTracker> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task TrackPostViewAsync(HttpContext http, Post post, CancellationToken ct = default)
    {
        var hash = VisitorIdentity.ComputeHash(http);
        var profile = ClientHints.From(http);
        var sessionKey = GetOrCreateSessionKey(http);

        var windowStart = DateTime.UtcNow - VisitorIdentity.DedupeWindow;
        var already = await _db.PostViews.AnyAsync(v =>
            v.PostId == post.Id && v.VisitorHash == hash && v.ViewedAtUtc >= windowStart, ct);
        if (already) return;

        post.ViewCount++;
        var pv = new PostView
        {
            PostId = post.Id,
            ViewedAtUtc = DateTime.UtcNow,
            VisitorHash = hash,
            SessionKey = sessionKey,
            DeviceType = profile.DeviceType,
            Browser = profile.Browser,
            Os = profile.Os,
            TrafficSource = profile.TrafficSource,
            ReferrerHost = profile.ReferrerHost,
            CountryCode = profile.CountryCode
        };
        _db.PostViews.Add(pv);

        var session = await _db.AnalyticsSessions.FirstOrDefaultAsync(s => s.SessionKey == sessionKey, ct);
        if (session is null)
        {
            session = new AnalyticsSession
            {
                SessionKey = sessionKey,
                VisitorHash = hash,
                StartedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
                PageViewCount = 1,
                DeviceType = profile.DeviceType,
                Browser = profile.Browser,
                Os = profile.Os,
                CountryCode = profile.CountryCode,
                TrafficSource = profile.TrafficSource,
                ReferrerHost = profile.ReferrerHost
            };
            _db.AnalyticsSessions.Add(session);
        }
        else
        {
            session.PageViewCount++;
            session.LastSeenAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task TrackSearchAsync(HttpContext http, string query, int resultCount, CancellationToken ct = default)
    {
        query = (query ?? "").Trim();
        if (query.Length is < 1 or > 200) return;

        _db.SearchQueryLogs.Add(new SearchQueryLog
        {
            Query = query,
            ResultCount = resultCount,
            SearchedAtUtc = DateTime.UtcNow,
            VisitorHash = VisitorIdentity.ComputeHash(http)
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task TrackReadingDurationAsync(int postId, int seconds, string? visitorHash, CancellationToken ct = default)
    {
        if (seconds < 3 || seconds > 3600 * 4) return;
        if (!await _db.Posts.AnyAsync(p => p.Id == postId, ct)) return;

        _db.ReadingDurationLogs.Add(new ReadingDurationLog
        {
            PostId = postId,
            DurationSeconds = seconds,
            LoggedAtUtc = DateTime.UtcNow,
            VisitorHash = visitorHash
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task TrackHeatmapClickAsync(int postId, int x, int y, CancellationToken ct = default)
    {
        x = Math.Clamp(x, 0, 1000);
        y = Math.Clamp(y, 0, 1000);
        if (!await _db.Posts.AnyAsync(p => p.Id == postId, ct)) return;

        _db.HeatmapClicks.Add(new HeatmapClick
        {
            PostId = postId,
            X = x,
            Y = y,
            ClickedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string GetOrCreateSessionKey(HttpContext http)
    {
        const string cookie = "Blog.Sid";
        if (http.Request.Cookies.TryGetValue(cookie, out var existing)
            && !string.IsNullOrEmpty(existing)
            && existing.Length <= 64)
            return existing;

        var key = Convert.ToHexString(Guid.NewGuid().ToByteArray());
        http.Response.Cookies.Append(cookie, key, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = http.Request.IsHttps,
            MaxAge = TimeSpan.FromHours(4)
        });
        return key;
    }
}
