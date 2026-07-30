using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public partial class ApplicationDbContext
{
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<ApiRequestLog> ApiRequestLogs => Set<ApiRequestLog>();

    private void ConfigureApi(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKey>(e =>
        {
            e.HasIndex(k => k.KeyHash).IsUnique();
            e.HasIndex(k => k.UserId);
            e.HasOne(k => k.User).WithMany().HasForeignKey(k => k.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WebhookSubscription>(e =>
        {
            e.HasIndex(w => w.UserId);
            e.HasOne(w => w.ApiKey).WithMany().HasForeignKey(w => w.ApiKeyId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ApiRequestLog>(e =>
        {
            e.HasIndex(l => l.CreatedAtUtc);
            e.HasIndex(l => l.UserId);
            e.HasIndex(l => l.ApiKeyId);
            e.HasIndex(l => l.StatusCode);
            e.HasOne(l => l.ApiKey).WithMany().HasForeignKey(l => l.ApiKeyId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
