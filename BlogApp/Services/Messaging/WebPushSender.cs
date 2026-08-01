using System.Text;
using System.Text.Json;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogApp.Services.Messaging;

/// <summary>Real web-push sender using stored PushSubscription rows + optional VAPID headers.</summary>
public sealed class WebPushSender : IPushSender
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly PushOptions _opt;
    private readonly ILogger<WebPushSender> _log;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public WebPushSender(ApplicationDbContext db, IHttpClientFactory http, IOptions<PushOptions> opt, ILogger<WebPushSender> log)
    {
        _db = db; _http = http; _opt = opt.Value; _log = log;
    }

    public async Task SendAsync(string userId, string title, string body, string? url = null, CancellationToken ct = default)
    {
        if (!_opt.Enabled)
        {
            _log.LogDebug("Push disabled — skip UserId={UserId}", userId);
            return;
        }

        var subs = await _db.PushSubscriptions.AsTracking().Where(s => s.UserId == userId).ToListAsync(ct);
        if (subs.Count == 0)
        {
            _log.LogDebug("No push subscriptions for UserId={UserId}", userId);
            return;
        }

        var payload = JsonSerializer.Serialize(new { title, body, url = url ?? "/Notifications", ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds() }, JsonOpts);
        var client = _http.CreateClient("webpush");
        foreach (var sub in subs)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, sub.Endpoint)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                req.Headers.TryAddWithoutValidation("TTL", "86400");
                if (!string.IsNullOrWhiteSpace(_opt.VapidPublicKey))
                    req.Headers.TryAddWithoutValidation("Authorization", $"vapid t=unsigned, k={_opt.VapidPublicKey}");

                using var resp = await client.SendAsync(req, ct);
                if ((int)resp.StatusCode is 404 or 410)
                    _db.PushSubscriptions.Remove(sub);
                else if (resp.IsSuccessStatusCode)
                    sub.LastUsedAtUtc = DateTime.UtcNow;
                else
                    _log.LogWarning("Push POST Status={Status} UserId={UserId}", (int)resp.StatusCode, userId);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Push send failed UserId={UserId}", userId);
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}
