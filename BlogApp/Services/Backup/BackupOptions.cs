namespace BlogApp.Services.Backup;

/// <summary>
/// Application data backup policy (RPO / retention / storage on Docker volume).
/// Bound from configuration section "Backup".
/// </summary>
public sealed class BackupOptions
{
    public const string Section = "Backup";

    /// <summary>Master switch for scheduled backups.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Directory for backup zip files. In Docker defaults to /app/data/backups
    /// (same named volume as SQLite) so backups survive container recreation.
    /// </summary>
    public string Path { get; set; } = "/app/data/backups";

    /// <summary>How often the hosted worker creates a full snapshot (hours). Target RPO ≈ this interval.</summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>Delete backups older than this many days (retention policy).</summary>
    public int RetentionDays { get; set; } = 14;

    /// <summary>Maximum number of backup files to keep (0 = unlimited by count).</summary>
    public int MaxFiles { get; set; } = 30;

    /// <summary>Include SQLite database file (blog.db + WAL companions when present).</summary>
    public bool IncludeDatabase { get; set; } = true;

    /// <summary>Include /app/data tree except the backups folder itself (CMS state files).</summary>
    public bool IncludeDataDirectory { get; set; } = true;
}
