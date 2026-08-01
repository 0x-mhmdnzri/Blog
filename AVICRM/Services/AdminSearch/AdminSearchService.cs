using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using AVICRM.Data;
using AVICRM.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AVICRM.Services.AdminSearch;

/// <summary>Low-latency admin search over denormalized documents + memory cache.</summary>
public sealed class AdminSearchService
{
    private static readonly Regex Ws = new(@"\s+", RegexOptions.Compiled);
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AdminSearchService> _logger;

    public AdminSearchService(ApplicationDbContext db, IMemoryCache cache, ILogger<AdminSearchService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AdminSearchResponse> SearchAsync(AdminSearchRequest req, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var q = (req.Q ?? string.Empty).Trim();
        if (q.Length > 120) q = q[..120];
        var scope = string.IsNullOrWhiteSpace(req.Scope) ? "all" : req.Scope.Trim().ToLowerInvariant();
        var take = Math.Clamp(req.Take, 1, 50);
        var skip = Math.Max(0, req.Skip);

        if (q.Length < 1)
        {
            return new AdminSearchResponse
            {
                Query = q,
                Scope = scope,
                TookMs = (int)sw.ElapsedMilliseconds,
                Recent = GetRecentQueries(),
                Suggestions = GetDefaultSuggestions()
            };
        }

        var cacheKey = $"admin-search:{scope}:{q.ToLowerInvariant()}:{skip}:{take}";
        if (_cache.TryGetValue(cacheKey, out AdminSearchResponse? cached) && cached is not null)
        {
            cached.FromCache = true;
            cached.TookMs = (int)sw.ElapsedMilliseconds;
            return cached;
        }

        if (!await _db.AdminSearchDocuments.AsNoTracking().AnyAsync(ct))
            await RebuildIndexAsync(ct);

        var terms = Tokenize(q);
        IQueryable<AdminSearchDocument> baseQ = _db.AdminSearchDocuments.AsNoTracking();
        if (scope != "all")
            baseQ = baseQ.Where(d => d.EntityType == scope);

        foreach (var t in terms)
        {
            var term = t;
            baseQ = baseQ.Where(d =>
                d.Title.Contains(term)
                || (d.Subtitle != null && d.Subtitle.Contains(term))
                || (d.BodyText != null && d.BodyText.Contains(term))
                || d.EntityKey.Contains(term));
        }

        var matched = await baseQ
            .OrderByDescending(d => d.Boost)
            .ThenByDescending(d => d.UpdatedAtUtc)
            .Take(200)
            .ToListAsync(ct);

        var ranked = matched
            .Select(d => new { Doc = d, Score = Score(d, terms, q) })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Doc.Boost)
            .ThenByDescending(x => x.Doc.UpdatedAtUtc)
            .ToList();

        var total = ranked.Count;
        var page = ranked.Skip(skip).Take(take).ToList();
        var counts = ranked.GroupBy(x => x.Doc.EntityType).ToDictionary(g => g.Key, g => g.Count());

        var hits = page.Select(x => new AdminSearchHit
        {
            EntityType = x.Doc.EntityType,
            EntityKey = x.Doc.EntityKey,
            Title = x.Doc.Title,
            Subtitle = x.Doc.Subtitle,
            Snippet = BuildSnippet(x.Doc.BodyText ?? x.Doc.Subtitle, terms),
            Url = x.Doc.Url,
            Icon = x.Doc.Icon,
            Status = x.Doc.Status,
            UpdatedAtUtc = x.Doc.UpdatedAtUtc,
            Score = x.Score,
            RelativeTime = RelativeTime(x.Doc.UpdatedAtUtc)
        }).ToList();

        var response = new AdminSearchResponse
        {
            Query = q,
            Scope = scope,
            TotalHits = total,
            TotalHitsLabel = FormatHitCount(total),
            TookMs = (int)sw.ElapsedMilliseconds,
            Hits = hits,
            CountsByType = counts,
            Suggestions = BuildSuggestions(q, ranked.Select(r => r.Doc.Title).Take(8)),
            Recent = GetRecentQueries()
        };

        RememberQuery(q);
        _cache.Set(cacheKey, response, TimeSpan.FromSeconds(20));
        return response;
    }

    public async Task RebuildIndexAsync(CancellationToken ct = default)
    {
        _db.AdminSearchDocuments.RemoveRange(_db.AdminSearchDocuments);
        await _db.SaveChangesAsync(ct);

        var docs = new List<AdminSearchDocument>();

        var posts = await _db.Posts.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Author)
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .Take(2000)
            .ToListAsync(ct);

        foreach (var p in posts)
        {
            docs.Add(new AdminSearchDocument
            {
                EntityType = "post",
                EntityKey = p.Id.ToString(),
                Title = p.Title,
                Subtitle = $"{(p.IsPublished ? "published" : "draft")} · {p.Author?.DisplayName ?? p.AuthorId}",
                BodyText = Trunc((p.Summary ?? "") + " " + StripMd(p.ContentMarkdown), 8000),
                Url = $"/Posts/Edit/{p.Id}",
                Icon = "article",
                Status = p.IsPublished ? "published" : "draft",
                LanguageCode = p.LanguageCode,
                UpdatedAtUtc = p.UpdatedAtUtc,
                Boost = (p.IsSticky ? 20 : 0) + (p.IsFeatured ? 10 : 0) + Math.Min(p.ViewCount / 100, 15),
                FacetsJson = JsonSerializer.Serialize(new { category = p.Category?.Name, author = p.Author?.DisplayName })
            });
        }

        var comments = await _db.Comments.AsNoTracking()
            .Include(c => c.Post)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(1000)
            .ToListAsync(ct);

        foreach (var c in comments)
        {
            var body = GetCommentBody(c);
            docs.Add(new AdminSearchDocument
            {
                EntityType = "comment",
                EntityKey = c.Id.ToString(),
                Title = Trunc(body, 120),
                Subtitle = $"{c.Status} · on {c.Post?.Title ?? "#" + c.PostId}",
                BodyText = body,
                Url = "/Admin/Comments",
                Icon = "chat",
                Status = c.Status.ToString().ToLowerInvariant(),
                UpdatedAtUtc = c.CreatedAtUtc,
                Boost = c.Status == CommentStatus.Pending ? 8 : 0
            });
        }

        var users = await _db.Users.AsNoTracking().Take(500).ToListAsync(ct);
        foreach (var u in users)
        {
            docs.Add(new AdminSearchDocument
            {
                EntityType = "user",
                EntityKey = u.Id,
                Title = string.IsNullOrWhiteSpace(u.DisplayName) ? (u.UserName ?? u.Email ?? u.Id) : u.DisplayName,
                Subtitle = u.Email,
                BodyText = $"{u.UserName} {u.Email} {u.DisplayName} {u.Bio}",
                Url = "/AdminUsers",
                Icon = "person",
                Status = u.LockoutEnd > DateTimeOffset.UtcNow ? "locked" : "active",
                Boost = 5
            });
        }

        var media = await _db.MediaAssets.AsNoTracking()
            .OrderByDescending(m => m.UploadedAtUtc)
            .Take(800)
            .ToListAsync(ct);
        foreach (var m in media)
        {
            docs.Add(new AdminSearchDocument
            {
                EntityType = "media",
                EntityKey = m.Id.ToString(),
                Title = m.FileName,
                Subtitle = $"{m.ContentType} · {m.Kind}",
                BodyText = m.FileName,
                Url = "/Admin/Media",
                Icon = "image",
                Status = "ready",
                UpdatedAtUtc = m.UploadedAtUtc
            });
        }

        try
        {
            var themes = await _db.CustomThemes.AsNoTracking().Take(200).ToListAsync(ct);
            foreach (var t in themes)
            {
                docs.Add(new AdminSearchDocument
                {
                    EntityType = "theme",
                    EntityKey = t.Id.ToString(),
                    Title = t.Name,
                    Subtitle = t.Status.ToString(),
                    BodyText = t.Description,
                    Url = "/AdminThemes",
                    Icon = "palette",
                    Status = t.Status.ToString().ToLowerInvariant(),
                    UpdatedAtUtc = t.UpdatedAtUtc,
                    Boost = t.IsActive ? 12 : 0
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Theme index skipped");
        }

        foreach (var nav in AdminNavCatalog.All)
        {
            docs.Add(new AdminSearchDocument
            {
                EntityType = "page",
                EntityKey = nav.Key,
                Title = HumanizeNav(nav.LabelKey),
                Subtitle = $"{nav.Controller}/{nav.Action}",
                BodyText = $"{nav.Key} {nav.LabelKey} {nav.GroupKey}",
                Url = $"/{nav.Controller}/{nav.Action}",
                Icon = "page",
                Status = "nav",
                Boost = 30
            });
        }

        try
        {
            var cats = await _db.Categories.AsNoTracking().Take(200).ToListAsync(ct);
            foreach (var c in cats)
            {
                docs.Add(new AdminSearchDocument
                {
                    EntityType = "taxonomy",
                    EntityKey = $"cat-{c.Id}",
                    Title = c.Name,
                    Subtitle = "category",
                    BodyText = c.Slug,
                    Url = "/Taxonomy/Categories",
                    Icon = "tag",
                    Boost = 2
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Taxonomy index skipped");
        }

        _db.AdminSearchDocuments.AddRange(docs);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Admin search index rebuilt: {Count} docs", docs.Count);
    }

    private static string GetCommentBody(object c)
    {
        var t = c.GetType();
        foreach (var name in new[] { "Body", "Content", "Text", "Message" })
        {
            var p = t.GetProperty(name);
            if (p?.GetValue(c) is string s && !string.IsNullOrWhiteSpace(s))
                return s;
        }
        return "(comment)";
    }

    private static string HumanizeNav(string key)
    {
        var last = key.Contains('.') ? key[(key.LastIndexOf('.') + 1)..] : key;
        return last.Replace('_', ' ');
    }

    private static string[] Tokenize(string q)
        => Ws.Split(q.ToLowerInvariant()).Where(t => t.Length >= 1).Take(8).ToArray();

    private static double Score(AdminSearchDocument d, string[] terms, string raw)
    {
        double s = d.Boost;
        var title = d.Title.ToLowerInvariant();
        var sub = (d.Subtitle ?? "").ToLowerInvariant();
        var body = (d.BodyText ?? "").ToLowerInvariant();
        var rawL = raw.ToLowerInvariant();
        if (title == rawL) s += 100;
        else if (title.StartsWith(rawL)) s += 60;
        else if (title.Contains(rawL)) s += 40;
        foreach (var t in terms)
        {
            if (title.Contains(t)) s += 12;
            if (sub.Contains(t)) s += 6;
            if (body.Contains(t)) s += 2;
        }
        return s;
    }

    private static string? BuildSnippet(string? text, string[] terms)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var flat = Ws.Replace(text, " ").Trim();
        if (flat.Length <= 160) return flat;
        var idx = -1;
        foreach (var t in terms)
        {
            idx = flat.IndexOf(t, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) break;
        }
        if (idx < 0) return flat[..160] + "…";
        var start = Math.Max(0, idx - 40);
        var len = Math.Min(160, flat.Length - start);
        var snip = flat.Substring(start, len);
        if (start > 0) snip = "…" + snip;
        if (start + len < flat.Length) snip += "…";
        return snip;
    }

    private static string FormatHitCount(long n) => n switch
    {
        <= 0 => "No results",
        1 => "About 1 result",
        < 1000 => $"About {n} results",
        < 1_000_000 => $"About {n / 1000.0:0.#}K results",
        _ => $"About {n / 1_000_000.0:0.#}M results"
    };

    private static string? RelativeTime(DateTime? utc)
    {
        if (utc is null) return null;
        var span = DateTime.UtcNow - utc.Value;
        if (span.TotalSeconds < 60) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays}d";
        return utc.Value.ToString("yyyy-MM-dd");
    }

    private static string Trunc(string? s, int n)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = Ws.Replace(s, " ").Trim();
        return s.Length <= n ? s : s[..n];
    }

    private static string StripMd(string? md)
    {
        if (string.IsNullOrWhiteSpace(md)) return string.Empty;
        var s = Regex.Replace(md, @"[#*_`>~\[\]()!]", " ");
        return Ws.Replace(s, " ").Trim();
    }

    private static IReadOnlyList<string> BuildSuggestions(string q, IEnumerable<string> titles)
        => titles.Where(t => t.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();

    private static IReadOnlyList<string> GetDefaultSuggestions()
        => new[] { "posts", "comments", "users", "media", "settings", "analytics" };

    private static readonly LinkedList<string> Recent = new();
    private static readonly object RecentLock = new();

    private static void RememberQuery(string q)
    {
        lock (RecentLock)
        {
            Recent.Remove(q);
            Recent.AddFirst(q);
            while (Recent.Count > 8) Recent.RemoveLast();
        }
    }

    private static IReadOnlyList<string> GetRecentQueries()
    {
        lock (RecentLock) return Recent.ToList();
    }
}
