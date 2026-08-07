using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public partial class ApplicationDbContext
{
    public DbSet<BotCrawlHit> BotCrawlHits => Set<BotCrawlHit>();

    partial void ConfigureBotCrawl(ModelBuilder modelBuilder);
}

// Separate partial to keep OnModelCreating hooks tidy if needed later.
public partial class ApplicationDbContext
{
    private void ApplyBotCrawlModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BotCrawlHit>(e =>
        {
            e.HasIndex(h => h.HitAtUtc);
            e.HasIndex(h => new { h.BotFamily, h.HitAtUtc });
            e.HasIndex(h => h.StatusCode);
            e.HasIndex(h => h.BotKind);
            e.Property(h => h.BotFamily).HasMaxLength(40);
            e.Property(h => h.BotKind).HasMaxLength(16);
            e.Property(h => h.UserAgent).HasMaxLength(300);
            e.Property(h => h.Method).HasMaxLength(16);
            e.Property(h => h.Path).HasMaxLength(500);
            e.Property(h => h.Query).HasMaxLength(300);
            e.Property(h => h.IpHash).HasMaxLength(64);
        });
    }
}
