using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BlogApp.Data;
using BlogApp.Models.Enterprise;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogApp.Services.Backup;

/// <summary>
/// Full application-data backup to a directory on the Docker data volume
/// (default <c>/app/data/backups</c>). Uses SQLite online Backup API for a
/// consistent snapshot without stopping the app (supports RPO-oriented schedules).
/// </summary>
public sealed partial class AppBackupService : IAppBackupService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly IOptions<BackupOptions> _options;
    private readonly IAuditService _audit;
    private readonly ILogger<AppBackupService> _log;
    private readonly IWebHostEnvironment _env;

    public AppBackupService(
        ApplicationDbContext db,
        IConfiguration config,
        IOptions<BackupOptions> options,
        IAuditService audit,
        ILogger<AppBackupService> log,
        IWebHostEnvironment env)
    {
        _db = db;
        _config = config;
        _options = options;
        _audit = audit;
        _log = log;
        _env = env;
    }

    public string ResolveBackupDirectory()
    {
        var path = _options.Value.Path?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            path = "/app/data/backups";

        // Local/dev fallback when /app/data is not present
        if (path.StartsWith("/app/", StringComparison.Ordinal)
            && !Directory.Exists(Path.GetDirectoryName(path) ?? path)
            && !_env.IsProduction())
        {
            path = Path.Combine(_env.ContentRootPath, "App_Data", "backups");
        }

        Directory.CreateDirectory(path);
        return Path.GetFullPath(path);
    }

    public string? ResolveDatabasePath()
    {
        var cs = _config.GetConnectionString("DefaultConnection")
                 ?? "Data Source=blog.db;Cache=Shared;Pooling=True;Default Timeout=30";
        try
        {
            var builder = new SqliteConnectionStringBuilder(cs);
            var ds = builder.DataSource;
            if (string.IsNullOrWhiteSpace(ds)) return null;
            return Path.IsPathRooted(ds) ? ds : Path.GetFullPath(ds);
        }
        catch
        {
            return null;
        }
    }

    public async Task<BackupRecord> CreateFullBackupAsync(
        string actorId,
        string kind = "manual",
        CancellationToken ct = default)
    {
        var opts = _options.Value;
        var backupDir = ResolveBackupDirectory();
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var fileName = $"blog-full-{stamp}.zip";
        var zipPath = Path.Combine(backupDir, fileName);
        var staging = Path.Combine(backupDir, $".staging-{stamp}");
        Directory.CreateDirectory(staging);

        var included = new List<string>();
        long dbBytes = 0;

        try
        {
            if (opts.IncludeDatabase)
            {
                var dbPath = ResolveDatabasePath();
                if (dbPath is not null && File.Exists(dbPath))
                {
                    var snapshotPath = Path.Combine(staging, "blog.db");
                    await SnapshotSqliteAsync(dbPath, snapshotPath, ct);
                    dbBytes = new FileInfo(snapshotPath).Length;
                    included.Add("blog.db");
                }
                else
                {
                    _log.LogWarning("Database file not found at {Path}; backup continues without DB", dbPath);
                }
            }

            if (opts.IncludeDataDirectory)
            {
                var dataRoot = ResolveDataRoot();
                if (dataRoot is not null && Directory.Exists(dataRoot))
                {
                    var dataStaging = Path.Combine(staging, "data");
                    CopyDataTree(dataRoot, dataStaging, backupDir);
                    if (Directory.Exists(dataStaging))
                        included.Add("data/");
                }
            }

            var manifestPath = Path.Combine(staging, "manifest.json");
            var manifest = new
            {
                schema = 1,
                level = "full",
                kind,
                createdAtUtc = DateTime.UtcNow,
                actorId,
                rpoHours = opts.IntervalHours,
                retentionDays = opts.RetentionDays,
                includes = included,
                databasePath = ResolveDatabasePath(),
                host = Environment.MachineName
            };
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8,
                ct);

            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(staging, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            var fi = new FileInfo(zipPath);
            _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            var rec = new BackupRecord
            {
                FileName = fileName,
                SizeBytes = fi.Length,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = actorId,
                Kind = string.IsNullOrWhiteSpace(kind) ? "manual" : kind.Trim(),
                Notes = $"full zip; includes=[{string.Join(",", included)}]; dbBytes={dbBytes}"
            };
            _db.BackupRecords.Add(rec);
            await _db.SaveChangesAsync(ct);

            try { await _audit.LogAsync("backup.create", "Backup", rec.Id.ToString(), fileName); }
            catch { /* audit is best-effort */ }

            var removed = await EnforceRetentionAsync(ct);
            _log.LogInformation(
                "Backup created {File} size={Size}B actor={Actor} kind={Kind} purged={Purged}",
                fileName, fi.Length, actorId, kind, removed);

            return rec;
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Staging cleanup failed for {Staging}", staging);
            }
        }
    }

    public async Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken ct = default)
    {
        return await _db.BackupRecords.AsNoTracking()
            .OrderByDescending(b => b.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);
    }

    public async Task RestoreAsync(
        int backupId,
        string actorId,
        bool applySwap = false,
        CancellationToken ct = default)
    {
        var rec = await _db.BackupRecords.AsNoTracking()
                       .FirstOrDefaultAsync(b => b.Id == backupId, ct)
                   ?? throw new InvalidOperationException("Backup not found");

        var backupDir = ResolveBackupDirectory();
        var zipPath = Path.Combine(backupDir, rec.FileName);
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("Backup file missing on volume", zipPath);

        var staging = Path.Combine(backupDir, $".restore-{DateTime.UtcNow:yyyyMMddHHmmss}");
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);
        ZipFile.ExtractToDirectory(zipPath, staging);

        try { await _audit.LogAsync("backup.restore_staged", "Backup", backupId.ToString(), rec.FileName); }
        catch { /* best-effort */ }

        _log.LogWarning(
            "Backup {File} extracted to {Staging} by {Actor}; applySwap={Swap}",
            rec.FileName, staging, actorId, applySwap);

        if (!applySwap)
            return;

        var stagedDb = Path.Combine(staging, "blog.db");
        var liveDb = ResolveDatabasePath();
        if (!File.Exists(stagedDb) || liveDb is null)
            throw new InvalidOperationException("Staged database or live path missing; cannot swap");

        var liveDir = Path.GetDirectoryName(liveDb)!;
        Directory.CreateDirectory(liveDir);
        var tempLive = liveDb + ".restoring";
        File.Copy(stagedDb, tempLive, overwrite: true);

        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var side = liveDb + suffix;
            if (File.Exists(side))
            {
                try { File.Move(side, side + ".pre-restore", overwrite: true); }
                catch { /* ignore */ }
            }
        }

        File.Move(tempLive, liveDb, overwrite: true);
        _log.LogWarning("Live database swapped from backup {Id} by {Actor}; restart recommended", backupId, actorId);

        try { await _audit.LogAsync("backup.restore_applied", "Backup", backupId.ToString(), rec.FileName); }
        catch { /* best-effort */ }
    }

    public async Task<int> EnforceRetentionAsync(CancellationToken ct = default)
    {
        var opts = _options.Value;
        var dir = ResolveBackupDirectory();
        var removed = 0;
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, opts.RetentionDays));

        foreach (var file in Directory.EnumerateFiles(dir, "blog-*.zip", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTimeUtc < cutoff)
                {
                    info.Delete();
                    removed++;
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Could not delete old backup {File}", file);
            }
        }

        if (opts.MaxFiles > 0)
        {
            var ordered = Directory.EnumerateFiles(dir, "blog-*.zip", SearchOption.TopDirectoryOnly)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();
            foreach (var extra in ordered.Skip(opts.MaxFiles))
            {
                try
                {
                    extra.Delete();
                    removed++;
                }
                catch { /* ignore */ }
            }
        }

        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var records = await _db.BackupRecords.ToListAsync(ct);
        foreach (var r in records)
        {
            var path = Path.Combine(dir, r.FileName);
            if (!File.Exists(path) || r.CreatedAtUtc < cutoff)
                _db.BackupRecords.Remove(r);
        }
        await _db.SaveChangesAsync(ct);
        return removed;
    }

    private static async Task SnapshotSqliteAsync(string sourcePath, string destPath, CancellationToken ct)
    {
        await using var source = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        await source.OpenAsync(ct);

        try
        {
            await using var cmd = source.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(PASSIVE);";
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            /* non-fatal */
        }

        await using var dest = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = destPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        await dest.OpenAsync(ct);

        source.BackupDatabase(dest);
        await dest.CloseAsync();
        await source.CloseAsync();
    }

    private string? ResolveDataRoot()
    {
        var dbPath = ResolveDatabasePath();
        if (dbPath is null) return null;
        var dir = Path.GetDirectoryName(dbPath);
        return string.IsNullOrEmpty(dir) ? null : dir;
    }

    private static void CopyDataTree(string sourceRoot, string destRoot, string backupDir)
    {
        var backupFull = Path.GetFullPath(backupDir);
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var full = Path.GetFullPath(file);
            if (full.StartsWith(backupFull, StringComparison.OrdinalIgnoreCase))
                continue;
            var name = Path.GetFileName(full);
            if (name.Equals("blog.db", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("blog.db-", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("-wal", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("-shm", StringComparison.OrdinalIgnoreCase))
                continue;

            var rel = Path.GetRelativePath(sourceRoot, full);
            var dest = Path.Combine(destRoot, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(full, dest, overwrite: true);
        }
    }
}
