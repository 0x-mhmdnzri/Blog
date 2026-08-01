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
}
