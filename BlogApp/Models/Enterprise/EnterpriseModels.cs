using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models.Enterprise;

public class Tenant
{
    public int Id { get; set; }
    [MaxLength(80)] public string Code { get; set; } = string.Empty;
    [MaxLength(200)] public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<Workspace> Workspaces { get; set; } = new List<Workspace>();
    public ICollection<TenantDomain> Domains { get; set; } = new List<TenantDomain>();
}

public class Workspace
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    [MaxLength(80)] public string Code { get; set; } = string.Empty;
    [MaxLength(200)] public string Name { get; set; } = string.Empty;
    public bool IsIsolated { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class TenantDomain
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    [MaxLength(253)] public string Host { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool IsVerified { get; set; }
    [MaxLength(64)] public string? VerificationToken { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SsoProviderConfig
{
    public int Id { get; set; }
    public int? TenantId { get; set; }
    [MaxLength(40)] public string Protocol { get; set; } = "OIDC"; // OIDC | SAML
    [MaxLength(120)] public string DisplayName { get; set; } = string.Empty;
    [MaxLength(500)] public string Authority { get; set; } = string.Empty;
    [MaxLength(200)] public string ClientId { get; set; } = string.Empty;
    [MaxLength(500)] public string? ClientSecret { get; set; }
    [MaxLength(500)] public string? MetadataUrl { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum ApprovalState
{
    Draft = 0,
    Submitted = 1,
    InReview = 2,
    Approved = 3,
    Rejected = 4,
    Published = 5
}

public class ContentApprovalRequest
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public ApprovalState State { get; set; } = ApprovalState.Submitted;
    [MaxLength(450)] public string SubmittedByUserId { get; set; } = string.Empty;
    [MaxLength(450)] public string? ReviewerUserId { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
}

public enum LifecycleStage
{
    Active = 0,
    Review = 1,
    Archive = 2,
    Retire = 3
}

public class ContentLifecycleRecord
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public LifecycleStage Stage { get; set; } = LifecycleStage.Active;
    public DateTime? ReviewDueAtUtc { get; set; }
    public DateTime? ArchiveAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? UpdatedByUserId { get; set; }
}

public class LegalHold
{
    public int Id { get; set; }
    public int? PostId { get; set; }
    public Post? Post { get; set; }
    [MaxLength(450)] public string? UserId { get; set; }
    [MaxLength(200)] public string Reason { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime? ReleasedAtUtc { get; set; }
}

public class ConsentLog
{
    public int Id { get; set; }
    [MaxLength(450)] public string? UserId { get; set; }
    [MaxLength(200)] public string Email { get; set; } = string.Empty;
    [MaxLength(80)] public string Purpose { get; set; } = string.Empty; // marketing | cookies | terms
    public bool Granted { get; set; }
    [MaxLength(64)] public string? IpHash { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DataExportRequest
{
    public int Id { get; set; }
    [MaxLength(450)] public string UserId { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    [MaxLength(40)] public string Status { get; set; } = "pending"; // pending | ready | failed
    [MaxLength(500)] public string? FilePath { get; set; }
}

public class BackupRecord
{
    public int Id { get; set; }
    [MaxLength(260)] public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string CreatedByUserId { get; set; } = string.Empty;
    [MaxLength(40)] public string Kind { get; set; } = "manual"; // manual | scheduled
    [MaxLength(500)] public string? Notes { get; set; }
}

public class LocalizationEntry
{
    public int Id { get; set; }
    [MaxLength(160)] public string Key { get; set; } = string.Empty;
    [MaxLength(8)] public string LanguageCode { get; set; } = "fa";
    [MaxLength(80)] public string Group { get; set; } = "editorial";
    public string Value { get; set; } = string.Empty;
    [MaxLength(40)] public string Status { get; set; } = "draft"; // draft | review | published
    [MaxLength(450)] public string? AssigneeUserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
