using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public partial class ApplicationDbContext
{
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<SearchIndexEntry> SearchIndexEntries => Set<SearchIndexEntry>();

    private void ConfigurePerformance(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BackgroundJob>(e =>
        {
            e.HasIndex(j => new { j.Status, j.AvailableAtUtc });
            e.HasIndex(j => j.Type);
            e.Property(j => j.Payload).HasColumnType("TEXT");
            e.Property(j => j.LastError).HasMaxLength(2000);
        });

        modelBuilder.Entity<SearchIndexEntry>(e =>
        {
            e.HasIndex(s => s.PostId).IsUnique();
            e.HasIndex(s => new { s.LanguageCode, s.IsPublished });
            e.Property(s => s.BodyText).HasColumnType("TEXT");
            e.Property(s => s.Summary).HasColumnType("TEXT");
            e.HasOne(s => s.Post).WithMany().HasForeignKey(s => s.PostId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
