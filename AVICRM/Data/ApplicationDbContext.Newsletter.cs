using AVICRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

public partial class ApplicationDbContext
{
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();
    public DbSet<NewsletterSegment> NewsletterSegments => Set<NewsletterSegment>();
    public DbSet<NewsletterCampaign> NewsletterCampaigns => Set<NewsletterCampaign>();

    partial void OnModelCreatingNewsletter(ModelBuilder modelBuilder);

    // Called from main OnModelCreating if we add the call; also configure here via extension method style.
    public static void ConfigureNewsletter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NewsletterSubscriber>(e =>
        {
            e.HasIndex(s => s.Email).IsUnique();
            e.HasIndex(s => s.Status);
            e.HasIndex(s => s.ConfirmToken);
            e.HasIndex(s => s.UnsubscribeToken);
        });

        modelBuilder.Entity<NewsletterCampaign>(e =>
        {
            e.Property(c => c.BodyHtml).HasColumnType("TEXT");
            e.Property(c => c.BodyText).HasColumnType("TEXT");
            e.HasOne(c => c.Segment).WithMany().HasForeignKey(c => c.SegmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(c => c.Status);
            e.HasIndex(c => c.ScheduledAtUtc);
        });
    }
}
