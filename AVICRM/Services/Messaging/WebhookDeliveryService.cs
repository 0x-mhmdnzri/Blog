using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AVICRM.Data;
using AVICRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Services.Messaging;

/// <summary>Payload published on the bus for async webhook fan-out.</summary>
public sealed class WebhookDispatchMessage
{
    public string EventType { get; set; } = "";
    public string? UserId { get; set; }
    public int? NotificationId { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }
    public string? Kind { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object?> Extra { get; set; } = new();
}

public interface IWebhookDeliveryService
{
    Task DispatchAsync(WebhookDispatchMessage message, CancellationToken ct = default);
}

/// <summary>
/// Delivers signed JSON payloads to active WebhookSubscription rows matching event type.
/// HMAC-SHA256 over body using subscription Secret (header X-Blog-Signature).
/// </summary>
public sealed class WebhookDeliveryService : IWebhookDeliveryService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<WebhookDeliveryService> _log;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public WebhookDeliveryService(
        ApplicationDbContext db,
        IHttpClientFactory http,
        ILogger<WebhookDeliveryService> log)
    {
        _db = db;
        _http = http;
        _log = log;
    }

    public async Task DispatchAsync(WebhookDispatchMessage message, CancellationToken ct = default)
    {
        var subs = await _db.WebhookSubscriptions.AsNoTracking()
            .Where(w => w.IsActive)
            .ToListAsync(ct);

        var matched = subs.Where(w => EventMatches(w.Events, message.EventType)).ToList();
        if (matched.Count == 0) return;

        var payload = JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid().ToString("N"),
            type = message.EventType,
            occurredAtUtc = message.OccurredAtUtc,
            data = new
            {
                notificationId = message.NotificationId,
                userId = message.UserId,
                title = message.Title,
                body = message.Body,
                linkUrl = message.LinkUrl,
                kind = message.Kind,
                extra = message.Extra
            }
        }, JsonOpts);

        var client = _http.CreateClient("webhooks");
        foreach (var sub in matched)
        {
            await DeliverOneAsync(client, sub, message.EventType, payload, attempt: 1, ct);
        }
    }

    private static bool EventMatches(string eventsCsv, string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventsCsv) || eventsCsv.Trim() == "*")
            return true;
        var parts = eventsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Any(p => string.Equals(p, eventType, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(p, "*", StringComparison.OrdinalIgnoreCase));
    }

    private async Task DeliverOneAsync(
        HttpClient client,
        WebhookSubscription sub,
        string eventType,
        string payload,
        int attempt,
        CancellationToken ct)
    {
        var delivery = new WebhookDelivery
        {
            SubscriptionId = sub.Id,
            EventType = eventType,
            TargetUrl = sub.TargetUrl,
            Attempt = attempt,
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, sub.TargetUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.TryAddWithoutValidation("X-Blog-Event", eventType);
            req.Headers.TryAddWithoutValidation("X-Blog-Delivery", Guid.NewGuid().ToString("N"));
            if (!string.IsNullOrEmpty(sub.Secret))
            {
                var sig = Sign(payload, sub.Secret);
                req.Headers.TryAddWithoutValidation("X-Blog-Signature", "sha256=" + sig);
            }

            using var resp = await client.SendAsync(req, ct);
            delivery.HttpStatus = (int)resp.StatusCode;
            delivery.Success = resp.IsSuccessStatusCode;
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                delivery.Error = body.Length > 500 ? body[..500] : body;
                _log.LogWarning("Webhook delivery failed Sub={Sub} Status={Status}", sub.Id, delivery.HttpStatus);
            }
        }
        catch (Exception ex)
        {
            delivery.Success = false;
            delivery.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            _log.LogWarning(ex, "Webhook delivery exception Sub={Sub}", sub.Id);
        }

        _db.WebhookDeliveries.Add(delivery);
        await _db.SaveChangesAsync(ct);
    }

    private static string Sign(string body, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(body);
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
