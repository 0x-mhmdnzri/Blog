using BlogApp.Models.Enterprise;

namespace BlogApp.Services.Backup;

public interface IAppBackupService
{
    /// <summary>Create a full zip snapshot under the configured backup directory (Docker volume).</summary>
    Task<BackupRecord> CreateFullBackupAsync(string actorId, string kind = "manual", CancellationToken ct = default);

    Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Extract backup to staging and optionally swap the live SQLite file.
    /// When <paramref name="applySwap"/> is true the process should be recycled after return.
    /// </summary>
    Task RestoreAsync(int backupId, string actorId, bool applySwap = false, CancellationToken ct = default);

    /// <summary>Delete old files according to retention policy; returns number of files removed.</summary>
    Task<int> EnforceRetentionAsync(CancellationToken ct = default);

    string ResolveBackupDirectory();
    string? ResolveDatabasePath();

    /// <summary>Absolute path of a registered backup zip if the file still exists.</summary>
    string? GetBackupFilePath(int backupId);

    /// <summary>Remove backup record + file.</summary>
    Task<bool> DeleteBackupAsync(int backupId, string actorId, CancellationToken ct = default);

    /// <summary>Storage, volume and process I/O snapshot for SuperAdmin monitoring.</summary>
    BackupStorageSnapshot GetStorageSnapshot();
}

/// <summary>Point-in-time storage + I/O metrics (JSON-friendly).</summary>
public sealed class BackupStorageSnapshot
{
    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;

    public string BackupDirectory { get; init; } = "";
    public long BackupDirBytes { get; init; }
    public int BackupFileCount { get; init; }

    public string? DatabasePath { get; init; }
    public long DatabaseBytes { get; init; }
    public long DatabaseWalBytes { get; init; }

    public string? DataRoot { get; init; }
    public long DataRootBytes { get; init; }
    public long MediaBytes { get; init; }

    public string? VolumeRoot { get; init; }
    public long VolumeTotalBytes { get; init; }
    public long VolumeFreeBytes { get; init; }
    public long VolumeUsedBytes { get; init; }
    public double VolumeUsedPercent { get; init; }

    /// <summary>Process cumulative read bytes (/proc/self/io on Linux).</summary>
    public long ProcessReadBytes { get; init; }
    /// <summary>Process cumulative write bytes.</summary>
    public long ProcessWriteBytes { get; init; }
    public bool ProcessIoAvailable { get; init; }

    public int RpoHours { get; init; }
    public int RetentionDays { get; init; }
    public int MaxFiles { get; init; }
    public int TargetRtoMinutes { get; init; }
    public bool ScheduledEnabled { get; init; }
}
