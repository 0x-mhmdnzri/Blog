using AVICRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

public partial class ApplicationDbContext
{
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<PostReaction> PostReactions => Set<PostReaction>();
    public DbSet<CategoryFollow> CategoryFollows => Set<CategoryFollow>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<UserMention> UserMentions => Set<UserMention>();

    private void OnModelCreatingSocial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PostLike>(e =>
        {
            e.HasKey(x => new { x.PostId, x.UserId });
            e.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PostReaction>(e =>
        {
            e.HasKey(x => new { x.PostId, x.UserId });
            e.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.PostId);
        });

        modelBuilder.Entity<CategoryFollow>(e =>
        {
            e.HasKey(x => new { x.CategoryId, x.UserId });
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserActivity>(e =>
        {
            e.HasIndex(x => x.ActorUserId);
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserMention>(e =>
        {
            e.HasIndex(x => x.MentionedUserId);
        });
    }
}
