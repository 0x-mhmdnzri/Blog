using AVICRM.Data;
using AVICRM.Models;

namespace AVICRM.Services;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string? entityType = null,
        string? entityId = null,
        string? details = null,
        HttpContext? http = null,
        CancellationToken ct = default);
}

public sealed class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;

    public AuditService(ApplicationDbContext db) => _db = db;

    public async Task LogAsync(
        string action,
        string? entityType = null,
        string? entityId = null,
        string? details = null,
        HttpContext? http = null,
        CancellationToken ct = default)
    {
        string? userId = null;
        string? userName = null;
        string? ip = null;

        if (http is not null)
        {
            userId = AuthorAccess.UserId(http.User);
            userName = http.User.Identity?.Name;
            ip = http.Connection.RemoteIpAddress?.ToString();
            var forwarded = http.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwarded))
                ip = forwarded.Split(',')[0].Trim();
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActorUserName = userName,
            Action = action.Length > 80 ? action[..80] : action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details is { Length: > 1000 } ? details[..1000] : details,
            IpAddress = ip,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
