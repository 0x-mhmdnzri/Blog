using AVICRM.Data;
using AVICRM.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AVICRM.Services.Messaging;

/// <summary>Sends weekly digest emails to users with WeeklyDigest preference.</summary>
public sealed class WeeklyDigestHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly DigestOptions _opt;
    private readonly ILogger<WeeklyDigestHostedService> _logger;
    private DateTime _lastRunDate = DateTime.MinValue;

    public WeeklyDigestHostedService(
        IServiceScopeFactory scopes,
        IOptions<DigestOptions> opt,
        ILogger<WeeklyDigestHostedService> logger)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opt.Enabled)
        {
            _logger.LogInformation("Weekly digest host disabled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                if ((int)now.DayOfWeek == _opt.DayOfWeekUtc
                    && now.Hour == _opt.HourUtc
                    && _lastRunDate.Date < now.Date)
                {
                    await RunDigestAsync(stoppingToken);
                    _lastRunDate = now.Date;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Weekly digest tick failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(20), stoppingToken);
        }
    }

    private async Task RunDigestAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var notify = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var since = DateTime.UtcNow.AddDays(-7);
        var posts = await db.Posts.AsNoTracking()
            .Where(p => p.IsPublished && !p.IsDeleted && p.PublishedAtUtc >= since)
            .OrderByDescending(p => p.ViewCount)
            .Take(10)
            .Select(p => new { p.Title, p.Slug, p.Summary, p.ViewCount })
            .ToListAsync(ct);

        if (posts.Count == 0)
        {
            _logger.LogInformation("Weekly digest skipped — no new posts");
            return;
        }

        var listHtml = string.Join("", posts.Select(p =>
            $"<li><strong>{System.Net.WebUtility.HtmlEncode(p.Title)}</strong> " +
            $"({p.ViewCount} بازدید) — {System.Net.WebUtility.HtmlEncode(p.Summary ?? "")}</li>"));

        var body = $"<h2>خلاصه هفتگی</h2><ul>{listHtml}</ul>";

        var recipients = await db.NotificationPreferences.AsNoTracking()
            .Where(p => p.WeeklyDigest && p.EmailEnabled)
            .Select(p => p.UserId)
            .ToListAsync(ct);

        var users = await db.Users.AsNoTracking()
            .Where(u => recipients.Contains(u.Id) && u.Email != null)
            .Select(u => new { u.Id, u.Email, u.DisplayName })
            .ToListAsync(ct);

        foreach (var u in users)
        {
            try
            {
                await email.SendAsync(u.Email!, "خلاصه هفتگی وبلاگ", body, true, ct);
                await notify.NotifyAsync(u.Id, NotificationKind.WeeklyDigest,
                    "خلاصه هفتگی ارسال شد", "۱۰ نوشته برتر هفته در ایمیل شماست.", "/", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Digest email failed UserId={UserId}", u.Id);
            }
        }

        _logger.LogInformation("Weekly digest sent Recipients={Count}", users.Count);
    }
}
