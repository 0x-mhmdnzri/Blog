using BlogApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<PostTag> PostTags => Set<PostTag>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<PostView> PostViews => Set<PostView>();
    public DbSet<PostRevision> PostRevisions => Set<PostRevision>();
    public DbSet<RedirectRule> RedirectRules => Set<RedirectRule>();
    public DbSet<BrokenLinkReport> BrokenLinkReports => Set<BrokenLinkReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Post>(e =>
        {
            e.HasIndex(p => p.Slug).IsUnique();
            e.Property(p => p.ContentMarkdown).HasColumnType("TEXT");
            e.HasOne(p => p.CoverMediaAsset)
                .WithMany()
                .HasForeignKey(p => p.CoverMediaAssetId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.Category)
                .WithMany(c => c.Posts)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.Author)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => p.IsDeleted);
            e.HasIndex(p => p.IsPublished);
            e.HasIndex(p => p.ScheduledPublishAtUtc);
            e.HasIndex(p => p.IsFeatured);
            e.HasIndex(p => p.IsSticky);
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
            e.HasIndex(v => new { v.PostId, v.VisitorHash, v.ViewedAtUtc });
            e.HasOne(v => v.Post)
                .WithMany(p => p.Views)
                .HasForeignKey(v => v.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PostRevision>(e =>
        {
            e.Property(r => r.ContentMarkdown).HasColumnType("TEXT");
            e.HasOne(r => r.Post)
                .WithMany(p => p.Revisions)
                .HasForeignKey(r => r.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.PostId);
            e.HasIndex(r => r.CreatedAtUtc);
        });

        modelBuilder.Entity<RedirectRule>(e =>
        {
            e.HasIndex(r => r.FromPath);
            e.HasIndex(r => r.IsActive);
        });

        modelBuilder.Entity<BrokenLinkReport>(e =>
        {
            e.HasOne(r => r.Post)
                .WithMany()
                .HasForeignKey(r => r.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.PostId);
        });

        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.ProfileImage).HasColumnType("BLOB");
        });
    }
}
