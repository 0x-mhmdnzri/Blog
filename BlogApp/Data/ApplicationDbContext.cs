using BlogApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<PostTag> PostTags => Set<PostTag>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<CommentLike> CommentLikes => Set<CommentLike>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<PostView> PostViews => Set<PostView>();
    public DbSet<PostRevision> PostRevisions => Set<PostRevision>();
    public DbSet<RedirectRule> RedirectRules => Set<RedirectRule>();
    public DbSet<BrokenLinkReport> BrokenLinkReports => Set<BrokenLinkReport>();
    public DbSet<PostSeries> PostSeries => Set<PostSeries>();
    public DbSet<SeriesPost> SeriesPosts => Set<SeriesPost>();
    public DbSet<TopicCollection> TopicCollections => Set<TopicCollection>();
    public DbSet<TopicCollectionItem> TopicCollectionItems => Set<TopicCollectionItem>();
    public DbSet<PostBookmark> PostBookmarks => Set<PostBookmark>();
    public DbSet<AppNotification> AppNotifications => Set<AppNotification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<AuthorFollow> AuthorFollows => Set<AuthorFollow>();
    public DbSet<OutboundMessage> OutboundMessages => Set<OutboundMessage>();
    public DbSet<NotificationCampaign> NotificationCampaigns => Set<NotificationCampaign>();
    public DbSet<AnalyticsSession> AnalyticsSessions => Set<AnalyticsSession>();
    public DbSet<SearchQueryLog> SearchQueryLogs => Set<SearchQueryLog>();
    public DbSet<ReadingDurationLog> ReadingDurationLogs => Set<ReadingDurationLog>();
    public DbSet<HeatmapClick> HeatmapClicks => Set<HeatmapClick>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<UiTranslation> UiTranslations => Set<UiTranslation>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<Advertisement> Advertisements => Set<Advertisement>();
    public DbSet<AffiliateLink> AffiliateLinks => Set<AffiliateLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Post>(e =>
        {
            e.HasIndex(p => new { p.LanguageCode, p.Slug }).IsUnique();
            e.HasIndex(p => p.LanguageCode);
            e.HasIndex(p => p.TranslationGroupId);
            e.Property(p => p.ContentMarkdown).HasColumnType("TEXT");
            e.Property(p => p.LanguageCode).HasMaxLength(8).HasDefaultValue(AppCultures.Default);
            e.HasOne(p => p.CoverMediaAsset).WithMany().HasForeignKey(p => p.CoverMediaAssetId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.Category).WithMany(c => c.Posts).HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.Author).WithMany(u => u.Posts).HasForeignKey(p => p.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => p.IsDeleted);
            e.HasIndex(p => p.IsPublished);
            e.HasIndex(p => p.ScheduledPublishAtUtc);
            e.HasIndex(p => p.IsFeatured);
            e.HasIndex(p => p.IsSticky);
            e.HasIndex(p => p.IsPremium);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.HasIndex(c => c.Slug).IsUnique();
            e.HasOne(c => c.Parent).WithMany(c => c.Children).HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Tag>().HasIndex(t => t.Slug).IsUnique();

        modelBuilder.Entity<PostTag>(e =>
        {
            e.HasKey(pt => new { pt.PostId, pt.TagId });
            e.HasOne(pt => pt.Post).WithMany(p => p.PostTags).HasForeignKey(pt => pt.PostId);
            e.HasOne(pt => pt.Tag).WithMany(t => t.PostTags).HasForeignKey(pt => pt.TagId);
        });

        modelBuilder.Entity<PostSeries>(e => e.HasIndex(s => s.Slug).IsUnique());

        modelBuilder.Entity<SeriesPost>(e =>
        {
            e.HasKey(sp => new { sp.SeriesId, sp.PostId });
            e.HasOne(sp => sp.Series).WithMany(s => s.Posts).HasForeignKey(sp => sp.SeriesId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(sp => sp.Post).WithMany(p => p.SeriesMemberships).HasForeignKey(sp => sp.PostId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TopicCollection>(e => e.HasIndex(t => t.Slug).IsUnique());

        modelBuilder.Entity<TopicCollectionItem>(e =>
        {
            e.HasOne(i => i.TopicCollection).WithMany(t => t.Items).HasForeignKey(i => i.TopicCollectionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Category).WithMany().HasForeignKey(i => i.CategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Tag).WithMany().HasForeignKey(i => i.TagId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MediaAsset>(e =>
        {
            e.Property(m => m.Content).HasColumnType("BLOB");
            e.HasOne(m => m.Post).WithMany(p => p.Media).HasForeignKey(m => m.PostId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Comment>(e =>
        {
            e.HasOne(c => c.Post).WithMany(p => p.Comments).HasForeignKey(c => c.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => c.LikeCount);
        });

        modelBuilder.Entity<CommentLike>(e =>
        {
            e.HasKey(l => new { l.CommentId, l.UserId });
            e.HasOne(l => l.Comment).WithMany(c => c.Likes).HasForeignKey(l => l.CommentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PostView>(e =>
        {
            e.HasIndex(v => v.ViewedAtUtc);
            e.HasIndex(v => new { v.PostId, v.VisitorHash, v.ViewedAtUtc });
            e.HasIndex(v => v.TrafficSource);
            e.HasIndex(v => v.DeviceType);
            e.HasIndex(v => v.CountryCode);
            e.HasOne(v => v.Post).WithMany(p => p.Views).HasForeignKey(v => v.PostId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnalyticsSession>(e =>
        {
            e.HasIndex(s => s.SessionKey).IsUnique();
            e.HasIndex(s => s.StartedAtUtc);
        });

        modelBuilder.Entity<SearchQueryLog>(e =>
        {
            e.HasIndex(s => s.SearchedAtUtc);
            e.HasIndex(s => s.Query);
        });

        modelBuilder.Entity<ReadingDurationLog>(e =>
        {
            e.HasOne(r => r.Post).WithMany().HasForeignKey(r => r.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.LoggedAtUtc);
        });

        modelBuilder.Entity<HeatmapClick>(e =>
        {
            e.HasOne(h => h.Post).WithMany().HasForeignKey(h => h.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(h => h.PostId);
        });

        modelBuilder.Entity<PostRevision>(e =>
        {
            e.Property(r => r.ContentMarkdown).HasColumnType("TEXT");
            e.HasOne(r => r.Post).WithMany(p => p.Revisions).HasForeignKey(r => r.PostId).OnDelete(DeleteBehavior.Cascade);
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
            e.HasOne(r => r.Post).WithMany().HasForeignKey(r => r.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.PostId);
        });

        modelBuilder.Entity<PostBookmark>(e =>
        {
            e.HasKey(b => new { b.UserId, b.PostId });
            e.HasOne(b => b.User).WithMany(u => u.Bookmarks).HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(b => b.Post).WithMany().HasForeignKey(b => b.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(b => b.UserId);
            e.HasIndex(b => b.CreatedAtUtc);
        });

        modelBuilder.Entity<AppNotification>(e =>
        {
            e.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(n => new { n.UserId, n.IsRead });
            e.HasIndex(n => n.CreatedAtUtc);
        });

        modelBuilder.Entity<NotificationPreference>(e =>
        {
            e.HasKey(p => p.UserId);
            e.HasOne(p => p.User).WithOne().HasForeignKey<NotificationPreference>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuthorFollow>(e =>
        {
            e.HasKey(f => new { f.FollowerUserId, f.AuthorUserId });
            e.HasOne(f => f.Follower).WithMany().HasForeignKey(f => f.FollowerUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.Author).WithMany().HasForeignKey(f => f.AuthorUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OutboundMessage>(e =>
        {
            e.Property(m => m.Body).HasColumnType("TEXT");
            e.HasIndex(m => m.IsSent);
            e.HasIndex(m => m.CreatedAtUtc);
        });

        modelBuilder.Entity<NotificationCampaign>(e =>
        {
            e.HasIndex(c => new { c.IsSent, c.ScheduledAtUtc });
            e.HasIndex(c => c.CreatedAtUtc);
        });

        modelBuilder.Entity<SiteSetting>(e =>
        {
            e.HasKey(s => s.Key);
            e.Property(s => s.Value).HasColumnType("TEXT");
        });

        modelBuilder.Entity<FeatureFlag>(e =>
        {
            e.HasKey(f => f.Key);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.CreatedAtUtc);
            e.HasIndex(a => a.Action);
            e.Property(a => a.Details).HasColumnType("TEXT");
        });

        modelBuilder.Entity<ContentReport>(e =>
        {
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.CreatedAtUtc);
            e.Property(r => r.Details).HasColumnType("TEXT");
        });

        modelBuilder.Entity<UiTranslation>(e =>
        {
            e.HasIndex(t => new { t.Key, t.LanguageCode }).IsUnique();
            e.HasIndex(t => t.LanguageCode);
            e.HasIndex(t => t.Group);
            e.Property(t => t.Value).HasColumnType("TEXT");
        });

        modelBuilder.Entity<SubscriptionPlan>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.Price).HasColumnType("TEXT");
        });

        modelBuilder.Entity<UserSubscription>(e =>
        {
            e.HasOne(s => s.Plan).WithMany(p => p.Subscriptions).HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => s.UserId);
            e.HasIndex(s => s.Status);
        });

        modelBuilder.Entity<Donation>(e =>
        {
            e.Property(d => d.Amount).HasColumnType("TEXT");
            e.HasIndex(d => d.Status);
            e.HasIndex(d => d.CreatedAtUtc);
        });

        modelBuilder.Entity<Advertisement>(e =>
        {
            e.Property(a => a.HtmlContent).HasColumnType("TEXT");
            e.HasIndex(a => a.IsActive);
            e.HasIndex(a => a.Placement);
        });

        modelBuilder.Entity<AffiliateLink>(e =>
        {
            e.HasIndex(a => a.Code).IsUnique();
            e.HasIndex(a => a.IsActive);
        });

        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.ProfileImage).HasColumnType("BLOB");
        });

        OnModelCreatingSocial(modelBuilder);
    }
}
