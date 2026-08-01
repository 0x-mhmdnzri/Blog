using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.Enterprise;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace BlogApp.Services.Enterprise;

public sealed class EnterpriseService : IEnterpriseService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IHostEnvironment _env;
    private readonly IAuditService _audit;
    private readonly ILogger<EnterpriseService> _log;

    public EnterpriseService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        IHostEnvironment env,
        IAuditService audit,
        ILogger<EnterpriseService> log)
    {
        _db = db;
        _users = users;
        _env = env;
        _audit = audit;
        _log = log;
    }

    public async Task<IReadOnlyList<Tenant>> ListTenantsAsync(CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return await _db.Tenants.Include(t => t.Workspaces).Include(t => t.Domains)
            .OrderBy(t => t.Name).ToListAsync(ct);
    }

    public async Task<Tenant> CreateTenantAsync(string code, string name, CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var t = new Tenant
        {
            Code = Slug(code),
            Name = name.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Tenants.Add(t);
        await _db.SaveChangesAsync(ct);
        return t;
    }

    public async Task<Workspace> CreateWorkspaceAsync(int tenantId, string code, string name, bool isolated, CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var w = new Workspace
        {
            TenantId = tenantId,
            Code =Slug(code),
            Name = name.Trim(),
            IsIsolated = isolated,
            CreatedAtUtc = DateTime.UtcNow
        };
        // fix: space after =
        w.Code =Slug(code);
        w.Code = Slug(code);
        _db.Workspaces.Add(w);
        await _db.SaveChangesAsync(ct);
        return w;
    }

    public async Task<TenantDomain> AddDomainAsync(int tenantId, string host, bool primary, CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var d = new TenantDomain
        {
            TenantId = tenantId,
            Host = host.Trim().ToLowerInvariant(),
            IsPrimary = primary,
            VerificationToken = token,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.TenantDomains.Add(d);
        await _db.SaveChangesAsync(ct);
        return d;
    }

    public async Task<bool> VerifyDomainAsync(int domainId, string token, CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var d = await _db.TenantDomains.FirstOrDefaultAsync(x => x.Id == domainId, ct);
        if (d is null) return false;
        if (!string.Equals(d.VerificationToken, token, StringComparison.OrdinalIgnoreCase))
            return false;
        d.IsVerified = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SsoProviderConfig?> GetSsoAsync(int? tenantId = null, CancellationToken ct = default)
    {
        return await _db.SsoProviderConfigs.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SaveSsoAsync(SsoProviderConfig config, CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        config.UpdatedAtUtc = DateTime.UtcNow;
        if (config.Id == 0)
            _db.SsoProviderConfigs.Add(config);
        else
            _db.SsoProviderConfigs.Update(config);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ContentApprovalRequest> SubmitApprovalAsync(int postId, string userId, string? notes, CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var req = new ContentApprovalRequest
        {
            PostId = postId,
            SubmittedByUserId = userId,
            Notes = notes,
            State = ApprovalState.Submitted,
            SubmittedAtUtc = DateTime.UtcNow
        };
        _db.ContentApprovalRequests.Add(req);
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is not null)
        {
            post.IsPublished = false;
            post.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return req;
    }

    public async Task ResolveApprovalAsync(int requestId, string reviewerId, bool approve, string? notes, CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var req = await _db.ContentApprovalRequests.Include(r => r.Post)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new InvalidOperationException("Approval not found");

        if (await IsOnLegalHoldAsync(req.PostId, null, ct))
            throw new InvalidOperationException("Content is under legal hold");

        req.ReviewerUserId = reviewerId;
        req.Notes = notes ?? req.Notes;
        req.ResolvedAtUtc = DateTime.UtcNow;
        req.State = approve ? ApprovalState.Approved : ApprovalState.Rejected;

        if (approve && req.Post is not null)
        {
            req.Post.IsPublished = true;
            req.Post.PublishedAtUtc ??= DateTime.UtcNow;
            req.Post.UpdatedAtUtc = DateTime.UtcNow;
            req.State = ApprovalState.Published;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ContentApprovalRequest>> ListApprovalsAsync(ApprovalState? state = null, CancellationToken ct = default)
    {
        var q = _db.ContentApprovalRequests.AsNoTracking().Include(r => r.Post).AsQueryable();
        if (state is not null) q = q.Where(r => r.State == state);
        return await q.OrderByDescending(r => r.SubmittedAtUtc).Take(200).ToListAsync(ct);
    }

    public async Task SetLifecycleAsync(int postId, LifecycleStage stage, string? userId, DateTime? reviewDue, DateTime? archiveAt, CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var rec = await _db.ContentLifecycleRecords.FirstOrDefaultAsync(r => r.PostId == postId, ct);
        if (rec is null)
        {
            rec = new ContentLifecycleRecord { PostId = postId };
            _db.ContentLifecycleRecords.Add(rec);
        }
        rec.Stage = stage;
        rec.ReviewDueAtUtc = reviewDue;
        rec.ArchiveAtUtc = archiveAt;
        rec.UpdatedByUserId = userId;
        rec.UpdatedAtUtc = DateTime.UtcNow;

        if (stage == LifecycleStage.Archive)
        {
            var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId, ct);
            if (post is not null)
            {
                post.IsPublished = false;
                post.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public Task<ContentLifecycleRecord?> GetLifecycleAsync(int postId, CancellationToken ct = default) =>
        _db.ContentLifecycleRecords.AsNoTracking().FirstOrDefaultAsync(r => r.PostId == postId, ct);

    public async Task<LegalHold> PlaceLegalHoldAsync(int? postId, string? userId, string reason, string actorId, CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var h = new LegalHold
        {
            PostId = postId,
            UserId = userId,
            Reason = reason.Trim(),
            CreatedByUserId = actorId,
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };
        _db.LegalHolds.Add(h);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("legal_hold.place", "LegalHold", h.Id.ToString(), $"post={postId} user={userId} reason={reason}");
        return h;
    }

    public async Task ReleaseLegalHoldAsync(int holdId, string actorId, CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var h = await _db.LegalHolds.FirstOrDefaultAsync(x => x.Id == holdId, ct);
        if (h is null) return;
        h.IsActive = false;
        h.ReleasedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("legal_hold.release", "LegalHold", holdId.ToString(), $"actor={actorId}");
    }

    public Task<bool> IsOnLegalHoldAsync(int? postId, string? userId, CancellationToken ct = default)
    {
        return _db.LegalHolds.AsNoTracking().AnyAsync(h =>
            h.IsActive &&
            ((postId != null && h.PostId == postId) || (userId != null && h.UserId == userId)), ct);
    }

    public async Task LogConsentAsync(string email, string? userId, string purpose, bool granted, string? ipHash, CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        _db.ConsentLogs.Add(new ConsentLog
        {
            Email = email.Trim().ToLowerInvariant(),
            UserId = userId,
            Purpose = purpose,
            Granted = granted,
            IpHash = ipHash,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string> BuildGdprExportJsonAsync(string userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId);
        var posts = await _db.Posts.AsNoTracking().Where(p => p.AuthorId == userId)
            .Select(p => new { p.Id, p.Title, p.Slug, p.CreatedAtUtc, p.IsPublished }).ToListAsync(ct);
        var comments = await _db.Comments.AsNoTracking().Where(c => c.UserId == userId)
            .Select(c => new { c.Id, c.PostId, c.Body, c.CreatedAtUtc }).ToListAsync(ct);
        var consents = await _db.ConsentLogs.AsNoTracking().Where(c => c.UserId == userId).ToListAsync(ct);

        var payload = new
        {
            exportedAtUtc = DateTime.UtcNow,
            user = user is null ? null : new { user.Id, user.UserName, user.Email, user.PhoneNumber, user.DisplayName },
            posts,
            comments,
            consents
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var dir = Path.Combine(_env.ContentRootPath, "App_Data", "gdpr-exports");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{userId}-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        await File.WriteAllTextAsync(file, json, Encoding.UTF8, ct);

        _db.DataExportRequests.Add(new DataExportRequest
        {
            UserId = userId,
            Status = "ready",
            RequestedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
            FilePath = file
        });
        await _db.SaveChangesAsync(ct);
        return json;
    }

    public async Task EraseUserDataAsync(string userId, string actorId, CancellationToken ct = default)
    {
        if (await IsOnLegalHoldAsync(null, userId, ct))
            throw new InvalidOperationException("User is under legal hold and cannot be erased.");

        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        var comments = await _db.Comments.Where(c => c.UserId == userId).ToListAsync(ct);
        foreach (var c in comments)
        {
            c.Body = "[redacted]";
            c.AuthorName = "Deleted";
            c.AuthorEmail = null;
            c.UserId = null;
        }

        var user = await _users.FindByIdAsync(userId);
        if (user is not null)
        {
            var shortId = userId.Length >= 8 ? userId[..8] : userId;
            user.Email = $"erased-{shortId}@invalid.local";
            user.NormalizedEmail = user.Email.ToUpperInvariant();
            user.UserName = $"erased_{shortId}";
            user.NormalizedUserName = user.UserName.ToUpperInvariant();
            user.PhoneNumber = null;
            user.ProfileImage = null;
            user.DisplayName = "Deleted user";
            user.Bio = null;
            await _users.UpdateAsync(user);
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("gdpr.erase", "User", userId, $"actor={actorId}");
        _log.LogWarning("GDPR erase completed for {UserId} by {Actor}", userId, actorId);
    }

    public async Task<BackupRecord> CreateBackupAsync(string actorId, CancellationToken ct = default)
    {
        var dataDir = Path.Combine(_env.ContentRootPath, "App_Data", "backups");
        Directory.CreateDirectory(dataDir);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var fileName = $"blog-backup-{stamp}.zip";
        var zipPath = Path.Combine(dataDir, fileName);

        var candidates = new[]
        {
            Path.Combine(_env.ContentRootPath, "blog.db"),
            Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "blog.db")),
            Path.Combine(Directory.GetCurrentDirectory(), "blog.db")
        };
        var dbPath = candidates.FirstOrDefault(File.Exists);

        await using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            if (dbPath is not null)
                zip.CreateEntryFromFile(dbPath, "blog.db", CompressionLevel.Optimal);

            var meta = zip.CreateEntry("manifest.json");
            await using var w = new StreamWriter(meta.Open());
            await w.WriteAsync(JsonSerializer.Serialize(new
            {
                createdAtUtc = DateTime.UtcNow,
                actorId,
                includes = dbPath is null ? Array.Empty<string>() : new[] { "blog.db" }
            }));
        }

        var fi = new FileInfo(zipPath);
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var rec = new BackupRecord
        {
            FileName = fileName,
            SizeBytes = fi.Length,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actorId,
            Kind = "manual",
            Notes = "SQLite snapshot zip"
        };
        _db.BackupRecords.Add(rec);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("backup.create", "Backup", rec.Id.ToString(), fileName);
        return rec;
    }

    public async Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken ct = default) =>
        await _db.BackupRecords.AsNoTracking().OrderByDescending(b => b.CreatedAtUtc).Take(50).ToListAsync(ct);

    public async Task RestoreBackupAsync(int backupId, string actorId, CancellationToken ct = default)
    {
        var rec = await _db.BackupRecords.AsNoTracking().FirstOrDefaultAsync(b => b.Id == backupId, ct)
                  ?? throw new InvalidOperationException("Backup not found");

        var zipPath = Path.Combine(_env.ContentRootPath, "App_Data", "backups", rec.FileName);
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("Backup file missing", zipPath);

        var staging = Path.Combine(_env.ContentRootPath, "App_Data", "restore-staging");
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);
        ZipFile.ExtractToDirectory(zipPath, staging);

        await _audit.LogAsync("backup.restore_staged", "Backup", backupId.ToString(), rec.FileName);
        _log.LogWarning("Backup {File} extracted to staging by {Actor}; restart required to swap DB", rec.FileName, actorId);
    }

    public async Task UpsertLocalizationAsync(string key, string lang, string value, string status, string? assignee, CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var e = await _db.LocalizationEntries.FirstOrDefaultAsync(x => x.Key == key && x.LanguageCode == lang, ct);
        if (e is null)
        {
            e = new LocalizationEntry { Key = key, LanguageCode = lang };
            _db.LocalizationEntries.Add(e);
        }
        e.Value = value;
        e.Status = status;
        e.AssigneeUserId = assignee;
        e.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LocalizationEntry>> ListLocalizationAsync(string? lang = null, CancellationToken ct = default)
    {
        var q = _db.LocalizationEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(lang)) q = q.Where(x => x.LanguageCode == lang);
        return await q.OrderBy(x => x.Group).ThenBy(x => x.Key).Take(500).ToListAsync(ct);
    }

    private static string Slug(string s)
    {
        var t = new string(s.Trim().ToLowerInvariant().Select(c =>
            char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (t.Contains("--", StringComparison.Ordinal)) t = t.Replace("--", "-", StringComparison.Ordinal);
        return t.Trim('-');
    }
}
