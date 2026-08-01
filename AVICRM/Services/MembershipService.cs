using AVICRM.Data;
using AVICRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Services;

public interface IMembershipService
{
    Task<bool> HasActiveMembershipAsync(string? userId, CancellationToken ct = default);
    Task<UserSubscription?> GetActiveSubscriptionAsync(string userId, CancellationToken ct = default);
}

public sealed class MembershipService : IMembershipService
{
    private readonly ApplicationDbContext _db;

    public MembershipService(ApplicationDbContext db) => _db = db;

    public async Task<bool> HasActiveMembershipAsync(string? userId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId)) return false;
        var now = DateTime.UtcNow;
        return await _db.UserSubscriptions.AsNoTracking().AnyAsync(s =>
            s.UserId == userId
            && s.Status == SubscriptionStatus.Active
            && (s.EndsAtUtc == null || s.EndsAtUtc > now), ct);
    }

    public async Task<UserSubscription?> GetActiveSubscriptionAsync(string userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.UserSubscriptions.AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId
                        && s.Status == SubscriptionStatus.Active
                        && (s.EndsAtUtc == null || s.EndsAtUtc > now))
            .OrderByDescending(s => s.EndsAtUtc)
            .FirstOrDefaultAsync(ct);
    }
}
