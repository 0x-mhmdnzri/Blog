using AVICRM.Models.Enterprise;

namespace AVICRM.Services.Enterprise;

public interface IEnterpriseService
{
    Task<IReadOnlyList<Tenant>> ListTenantsAsync(CancellationToken ct = default);
    Task<Tenant> CreateTenantAsync(string code, string name, CancellationToken ct = default);
    Task<Workspace> CreateWorkspaceAsync(int tenantId, string code, string name, bool isolated, CancellationToken ct = default);
    Task<TenantDomain> AddDomainAsync(int tenantId, string host, bool primary, CancellationToken ct = default);
    Task<bool> VerifyDomainAsync(int domainId, string token, CancellationToken ct = default);

    Task<SsoProviderConfig?> GetSsoAsync(int? tenantId = null, CancellationToken ct = default);
    Task SaveSsoAsync(SsoProviderConfig config, CancellationToken ct = default);

    Task<ContentApprovalRequest> SubmitApprovalAsync(int postId, string userId, string? notes, CancellationToken ct = default);
    Task ResolveApprovalAsync(int requestId, string reviewerId, bool approve, string? notes, CancellationToken ct = default);
    Task<IReadOnlyList<ContentApprovalRequest>> ListApprovalsAsync(ApprovalState? state = null, CancellationToken ct = default);

    Task SetLifecycleAsync(int postId, LifecycleStage stage, string? userId, DateTime? reviewDue, DateTime? archiveAt, CancellationToken ct = default);
    Task<ContentLifecycleRecord?> GetLifecycleAsync(int postId, CancellationToken ct = default);

    Task<LegalHold> PlaceLegalHoldAsync(int? postId, string? userId, string reason, string actorId, CancellationToken ct = default);
    Task ReleaseLegalHoldAsync(int holdId, string actorId, CancellationToken ct = default);
    Task<bool> IsOnLegalHoldAsync(int? postId, string? userId, CancellationToken ct = default);

    Task LogConsentAsync(string email, string? userId, string purpose, bool granted, string? ipHash, CancellationToken ct = default);
    Task<string> BuildGdprExportJsonAsync(string userId, CancellationToken ct = default);
    Task EraseUserDataAsync(string userId, string actorId, CancellationToken ct = default);

    Task<BackupRecord> CreateBackupAsync(string actorId, CancellationToken ct = default);
    Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken ct = default);
    Task RestoreBackupAsync(int backupId, string actorId, CancellationToken ct = default);

    Task UpsertLocalizationAsync(string key, string lang, string value, string status, string? assignee, CancellationToken ct = default);
    Task<IReadOnlyList<LocalizationEntry>> ListLocalizationAsync(string? lang = null, CancellationToken ct = default);
}
