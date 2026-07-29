using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<PostTag> PostTags => Set<PostTag>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<PostView> PostViews => Set<PostView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Post>(e =>
        {
            e.HasIndex(p => p.Slug).IsUnique();
            e.Property(p => p.ContentMarkdown).HasColumnType("TEXT"); // unlimited length
            e.HasOne(p => p.CoverMediaAsset)
                .WithMany()
                .HasForeignKey(p => p.CoverMediaAssetId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.Category)
                .WithMany(c => c.Posts)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Category>().HasIndex(c => c.Slug).IsUnique();
        modelBuilder.Entity<Tag>().HasIndex(t => t.Slug).IsUnique();

        modelBuilder.Entity<PostTag>(e =>
        {
            e.HasKey(pt => new { pt.PostId, pt.TagId });
            e.HasOne(pt => pt.Post).WithMany(p => p.PostTags).HasForeignKey(pt => pt.PostId);
            e.HasOne(pt => pt.Tag).WithMany(t => t.PostTags).HasForeignKey(pt => pt.TagId);
        });

        modelBuilder.Entity<MediaAsset>(e =>
        {
            e.Property(m => m.Content).HasColumnType("BLOB");
            e.HasOne(m => m.Post)
                .WithMany(p => p.Media)
                .HasForeignKey(m => m.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PostView>(e =>
        {
            e.HasIndex(v => v.ViewedAtUtc);
            e.HasIndex(v => new { v.PostId, v.VisitorHash, v.ViewedAtUtc }); // powers the dedup check
            e.HasOne(v => v.Post)
                .WithMany(p => p.Views)
                .HasForeignKey(v => v.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
