using BlogApp.Models.Enterprise;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services.Backup;

public sealed partial class AppBackupService
{
    public string? GetBackupFilePath(int backupId)
    {
        var rec = _db.BackupRecords.AsNoTracking().FirstOrDefault(b => b.Id == backupId);
        if (rec is null) return null;
        var path = Path.Combine(ResolveBackupDirectory(), rec.FileName);
        return File.Exists(path) ? path : null;
    }

    public async Task<bool> DeleteBackupAsync(int backupId, string actorId, CancellationToken ct = default)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var rec = await _db.BackupRecords.FirstOrDefaultAsync(b => b.Id == backupId, ct);
        if (rec is null) return false;

        var path = Path.Combine(ResolveBackupDirectory(), rec.FileName);
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to delete backup file {Path}", path);
        }

        _db.BackupRecords.Remove(rec);
        await _db.SaveChangesAsync(ct);
        try { await _audit.LogAsync("backup.delete", "Backup", backupId.ToString(), rec.FileName); }
        catch { }
        return true;
    }

    public BackupStorageSnapshot GetStorageSnapshot()
    {
        var opts = _options.Value;
        var backupDir = ResolveBackupDirectory();
        long backupBytes = 0;
        var backupCount = 0;
        try
        {
            if (Directory.Exists(backupDir))
            {
                foreach (var f in Directory.EnumerateFiles(backupDir, "*.zip", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        backupBytes += new FileInfo(f).Length;
                        backupCount++;
                    }
                    catch { /* skip */ }
                }
            }
        }
        catch { /* skip */ }

        var dbPath = ResolveDatabasePath();
        long dbBytes = 0, walBytes = 0;
        if (dbPath is not null && File.Exists(dbPath))
        {
            try { dbBytes = new FileInfo(dbPath).Length; } catch { }
            foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
            {
                var p = dbPath + suffix;
                if (File.Exists(p))
                {
                    try { walBytes += new FileInfo(p).Length; } catch { }
                }
            }
        }

        var dataRoot = ResolveDataRoot();
        long dataBytes = 0, mediaBytes = 0;
        if (dataRoot is not null && Directory.Exists(dataRoot))
        {
            try
            {
                dataBytes = DirSizeSafe(dataRoot, backupDir);
                var mediaCandidates = new[]
                {
                    Path.Combine(dataRoot, "media"),
                    Path.Combine(dataRoot, "uploads"),
                    Path.Combine(_env.WebRootPath ?? "", "uploads"),
                    Path.Combine(_env.WebRootPath ?? "", "media")
                };
                foreach (var m in mediaCandidates.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    mediaBytes += DirSizeSafe(m, backupDir);
                }
            }
            catch { /* skip */ }
        }

        long volTotal = 0, volFree = 0;
        string? volRoot = null;
        try
        {
            var root = Path.GetPathRoot(backupDir) ?? backupDir;
            var di = new DriveInfo(root);
            if (di.IsReady)
            {
                volRoot = di.RootDirectory.FullName;
                volTotal = di.TotalSize;
                volFree = di.AvailableFreeSpace;
            }
        }
        catch
        {
            try
            {
                var probe = backupDir;
                for (var i = 0; i < 6 && !string.IsNullOrEmpty(probe); i++)
                {
                    var di = new DriveInfo(probe);
                    if (di.IsReady && di.TotalSize > 0)
                    {
                        volRoot = di.Name;
                        volTotal = di.TotalSize;
                        volFree = di.AvailableFreeSpace;
                        break;
                    }
                    probe = Path.GetDirectoryName(probe.TrimEnd(Path.DirectorySeparatorChar));
                }
            }
            catch { }
        }

        var volUsed = volTotal > 0 ? Math.Max(0, volTotal - volFree) : 0;
        var volPct = volTotal > 0 ? Math.Round(100.0 * volUsed / volTotal, 1) : 0;

        long procRead = 0, procWrite = 0;
        var ioOk = TryReadProcIo(out procRead, out procWrite);

        return new BackupStorageSnapshot
        {
            CapturedAtUtc = DateTime.UtcNow,
            BackupDirectory = backupDir,
            BackupDirBytes = backupBytes,
            BackupFileCount = backupCount,
            DatabasePath = dbPath,
            DatabaseBytes = dbBytes,
            DatabaseWalBytes = walBytes,
            DataRoot = dataRoot,
            DataRootBytes = dataBytes,
            MediaBytes = mediaBytes,
            VolumeRoot = volRoot,
            VolumeTotalBytes = volTotal,
            VolumeFreeBytes = volFree,
            VolumeUsedBytes = volUsed,
            VolumeUsedPercent = volPct,
            ProcessReadBytes = procRead,
            ProcessWriteBytes = procWrite,
            ProcessIoAvailable = ioOk,
            RpoHours = opts.IntervalHours,
            RetentionDays = opts.RetentionDays,
            MaxFiles = opts.MaxFiles,
            TargetRtoMinutes = opts.TargetRtoMinutes,
            ScheduledEnabled = opts.Enabled
        };
    }

    private static long DirSizeSafe(string root, string excludeDir)
    {
        long total = 0;
        var exclude = Path.GetFullPath(excludeDir);
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var full = Path.GetFullPath(file);
                    if (full.StartsWith(exclude, StringComparison.OrdinalIgnoreCase))
                        continue;
                    total += new FileInfo(full).Length;
                }
                catch { /* skip locked files */ }
            }
        }
        catch { }
        return total;
    }

    private static bool TryReadProcIo(out long readBytes, out long writeBytes)
    {
        readBytes = 0;
        writeBytes = 0;
        try
        {
            const string path = "/proc/self/io";
            if (!File.Exists(path)) return false;
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("read_bytes:", StringComparison.Ordinal))
                    long.TryParse(line.AsSpan(11).Trim(), out readBytes);
                else if (line.StartsWith("write_bytes:", StringComparison.Ordinal))
                    long.TryParse(line.AsSpan(12).Trim(), out writeBytes);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
