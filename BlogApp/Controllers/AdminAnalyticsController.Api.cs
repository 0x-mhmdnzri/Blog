using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AdminAnalyticsController
{
    /// <summary>Dedicated API usage monitor — SuperAdmin only.</summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Api(int range = 30, string? userId = null)
    {
        if (range is not (7 or 30 or 90)) range = 30;
        var panel = await BuildApiPanelAsync(range, userId);

        ViewData["Title"] = "API Analytics";
        ViewBag.Range = range;
        ViewBag.FilterUserId = userId;
        return View(panel);
    }

    private async Task<ApiAnalyticsPanel> BuildApiPanelAsync(int range, string? filterUserId = null)
    {
        var today = DateTime.UtcNow.Date;
        var rangeStart = today.AddDays(-(range - 1));

        var logsQ = _db.ApiRequestLogs.AsNoTracking()
            .Where(l => l.CreatedAtUtc >= rangeStart);

        if (!string.IsNullOrWhiteSpace(filterUserId))
            logsQ = logsQ.Where(l => l.UserId == filterUserId);

        var logs = await logsQ.OrderByDescending(l => l.CreatedAtUtc).Take(50_000).ToListAsync();

        var keys = await _db.ApiKeys.AsNoTracking().Include(k => k.User).ToListAsync();

        var byDay = new List<ChartPoint>();
        for (var d = rangeStart; d <= today; d = d.AddDays(1))
        {
            byDay.Add(new ChartPoint
            {
                Label = d.ToString("MM-dd"),
                Value = logs.Count(l => l.CreatedAtUtc.Date == d)
            });
        }

        var users = keys
            .GroupBy(k => k.UserId)
            .Select(g =>
            {
                var uid = g.Key;
                var uname = g.First().User?.UserName ?? uid;
                var userLogs = logs.Where(l => l.UserId == uid).ToList();
                return new ApiUserUsageRow
                {
                    UserId = uid,
                    UserName = uname ?? uid,
                    KeyCount = g.Count(),
                    ActiveKeys = g.Count(k => k.IsActive && !k.IsBanned),
                    BannedKeys = g.Count(k => k.IsBanned),
                    LifetimeRequests = g.Sum(k => k.RequestCount),
                    RangeRequests = userLogs.Count,
                    RangeErrors = userLogs.Count(l => l.IsError),
                    RangeRateLimited = userLogs.Count(l => l.IsRateLimited),
                    AvgDurationMs = userLogs.Count == 0 ? 0 : Math.Round(userLogs.Average(l => l.DurationMs), 1),
                    LastCallUtc = userLogs.Count == 0 ? g.Max(k => k.LastUsedAtUtc) : userLogs.Max(l => l.CreatedAtUtc)
                };
            })
            .OrderByDescending(u => u.RangeRequests)
            .ThenByDescending(u => u.LifetimeRequests)
            .ToList();

        // Include users who called API but no longer have keys
        foreach (var orphan in logs.Where(l => !string.IsNullOrEmpty(l.UserId) && users.All(u => u.UserId != l.UserId))
                     .GroupBy(l => l.UserId!))
        {
            var list = orphan.ToList();
            users.Add(new ApiUserUsageRow
            {
                UserId = orphan.Key,
                UserName = list.First().UserName ?? orphan.Key,
                KeyCount = 0,
                RangeRequests = list.Count,
                RangeErrors = list.Count(l => l.IsError),
                RangeRateLimited = list.Count(l => l.IsRateLimited),
                AvgDurationMs = Math.Round(list.Average(l => l.DurationMs), 1),
                LastCallUtc = list.Max(l => l.CreatedAtUtc)
            });
        }

        users = users.OrderByDescending(u => u.RangeRequests).ToList();

        var endpoints = logs
            .GroupBy(l => l.Method + " " + NormalizePath(l.Path))
            .Select(g => new ApiEndpointUsageRow
            {
                Method = g.First().Method,
                Path = NormalizePath(g.First().Path),
                Count = g.Count(),
                Errors = g.Count(x => x.IsError),
                AvgMs = Math.Round(g.Average(x => x.DurationMs), 1)
            })
            .OrderByDescending(e => e.Count)
            .Take(20)
            .ToList();

        var statuses = logs
            .GroupBy(l => l.StatusCode.ToString())
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        return new ApiAnalyticsPanel
        {
            TotalRequests = logs.Count,
            ErrorCount = logs.Count(l => l.IsError),
            RateLimitedCount = logs.Count(l => l.IsRateLimited),
            UniqueUsers = users.Count(u => u.RangeRequests > 0),
            ActiveKeys = keys.Count(k => k.IsActive && !k.IsBanned),
            BannedKeys = keys.Count(k => k.IsBanned),
            AvgDurationMs = logs.Count == 0 ? 0 : Math.Round(logs.Average(l => l.DurationMs), 1),
            RequestsByDay = byDay,
            Users = users,
            TopEndpoints = endpoints,
            StatusCodes = statuses
        };
    }

    private static string NormalizePath(string path)
    {
        // collapse numeric ids: /api/v1/webhooks/12 → /api/v1/webhooks/{id}
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out _)) parts[i] = "{id}";
        }
        return "/" + string.Join('/', parts);
    }
}
