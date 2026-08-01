using BlogApp.Models.Enterprise;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public partial class ApplicationDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<SsoProviderConfig> SsoProviderConfigs => Set<SsoProviderConfig>();
    public DbSet<ContentApprovalRequest> ContentApprovalRequests => Set<ContentApprovalRequest>();
    public DbSet<ContentLifecycleRecord> ContentLifecycleRecords => Set<ContentLifecycleRecord>();
    public DbSet<LegalHold> LegalHolds => Set<LegalHold>();
    public DbSet<ConsentLog> ConsentLogs => Set<ConsentLog>();
    public DbSet<DataExportRequest> DataExportRequests => Set<DataExportRequest>();
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();
    public DbSet<LocalizationEntry> LocalizationEntries => Set<LocalizationEntry>();

    partial void ConfigureEnterprise(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasIndex(t => t.Code).IsUnique();
            e.Property(t => t.Code).HasMaxLength(80);
            e.Property(t => t.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<Workspace>(e =>
        {
            e.HasIndex(w => new { w.TenantId, w.Code }).IsUnique();
            e.HasOne(w => w.Tenant).WithMany(t => t.Workspaces).HasForeignKey(w => w.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantDomain>(e =>
        {
            e.HasIndex(d => d.Host).IsUnique();
            e.HasOne(d => d.Tenant).WithMany(t => t.Domains).HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SsoProviderConfig>(e =>
        {
            e.HasIndex(s => s.TenantId);
            e.Property(s => s.Protocol).HasMaxLength(40);
        });

        modelBuilder.Entity<ContentApprovalRequest>(e =>
        {
            e.HasOne(r => r.Post).WithMany().HasForeignKey(r => r.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.State);
            e.HasIndex(r => r.SubmittedAtUtc);
        });

        modelBuilder.Entity<ContentLifecycleRecord>(e =>
        {
            e.HasIndex(r => r.PostId).IsUnique();
            e.HasOne(r => r.Post).WithMany().HasForeignKey(r => r.PostId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LegalHold>(e =>
        {
            e.HasIndex(h => h.IsActive);
            e.HasIndex(h => h.PostId);
            e.HasIndex(h => h.UserId);
            e.HasOne(h => h.Post).WithMany().HasForeignKey(h => h.PostId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ConsentLog>(e =>
        {
            e.HasIndex(c => c.Email);
            e.HasIndex(c => c.CreatedAtUtc);
            e.HasIndex(c => c.Purpose);
        });

        modelBuilder.Entity<DataExportRequest>(e =>
        {
            e.HasIndex(r => r.UserId);
            e.HasIndex(r => r.Status);
        });

        modelBuilder.Entity<BackupRecord>(e =>
        {
            e.HasIndex(b => b.CreatedAtUtc);
        });

        modelBuilder.Entity<LocalizationEntry>(e =>
        {
            e.HasIndex(x => new { x.Key, x.LanguageCode }).IsUnique();
            e.Property(x => x.Value).HasColumnType("TEXT");
        });
    }
}
