using AVICRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

public partial class ApplicationDbContext
{
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<SearchIndexEntry> SearchIndexEntries => Set<SearchIndexEntry>();
    public DbSet<AdminSearchDocument> AdminSearchDocuments => Set<AdminSearchDocument>();
    public DbSet<MediaVariant> MediaVariants => Set<MediaVariant>();
    public DbSet<MediaVersion> MediaVersions => Set<MediaVersion>();

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
            e.HasIndex(s => s.AuthorUserId);
            e.Property(s => s.BodyText).HasColumnType("TEXT");
            e.Property(s => s.Summary).HasColumnType("TEXT");
            e.Property(s => s.AuthorUserId).HasMaxLength(450);
            e.Property(s => s.AuthorName).HasMaxLength(200);
            e.HasOne(s => s.Post).WithMany().HasForeignKey(s => s.PostId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminSearchDocument>(e =>
        {
            e.HasIndex(d => new { d.EntityType, d.EntityKey }).IsUnique();
            e.HasIndex(d => d.EntityType);
            e.HasIndex(d => d.UpdatedAtUtc);
            e.Property(d => d.BodyText).HasColumnType("TEXT");
            e.Property(d => d.FacetsJson).HasColumnType("TEXT");
        });

        modelBuilder.Entity<MediaVariant>(e =>
        {
            e.Property(v => v.Content).HasColumnType("BLOB");
            e.HasIndex(v => new { v.MediaAssetId, v.Width });
            e.HasOne(v => v.MediaAsset).WithMany(m => m.Variants).HasForeignKey(v => v.MediaAssetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MediaVersion>(e =>
        {
            e.Property(v => v.Content).HasColumnType("BLOB");
            e.HasIndex(v => v.MediaAssetId);
            e.HasOne(v => v.MediaAsset).WithMany(m => m.Versions).HasForeignKey(v => v.MediaAssetId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
