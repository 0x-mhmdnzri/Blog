using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public partial class ApplicationDbContext
{
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<SearchIndexEntry> SearchIndexEntries => Set<SearchIndexEntry>();

    partial void OnModelCreatingPerformance(ModelBuilder modelBuilder);
}
