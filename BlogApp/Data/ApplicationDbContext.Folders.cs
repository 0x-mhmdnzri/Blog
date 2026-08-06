using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public partial class ApplicationDbContext
{
    public DbSet<PostFolder> PostFolders => Set<PostFolder>();
    public DbSet<PostFolderItem> PostFolderItems => Set<PostFolderItem>();

    private void ConfigureFolders(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PostFolder>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.OwnerUserId);
            e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PostFolderItem>(e =>
        {
            e.HasKey(x => new { x.FolderId, x.PostId });
            e.HasOne(x => x.Folder).WithMany(x => x.Items).HasForeignKey(x => x.FolderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
