using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public partial class ApplicationDbContext
{
    public DbSet<BacklinkLead> BacklinkLeads => Set<BacklinkLead>();
    public DbSet<AuthoritySnapshot> AuthoritySnapshots => Set<AuthoritySnapshot>();

    partial void ConfigureAuthority(ModelBuilder modelBuilder);

    private void ApplyAuthorityModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BacklinkLead>(e =>
        {
            e.ToTable("BacklinkLeads");
            e.HasKey(x => x.Id);
            e.Property(x => x.TargetSite).HasMaxLength(300).IsRequired();
            e.Property(x => x.Status).HasMaxLength(24).IsRequired();
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<AuthoritySnapshot>(e =>
        {
            e.ToTable("AuthoritySnapshots");
            e.HasKey(x => x.Id);
            e.Property(x => x.Period).HasMaxLength(16).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.Period);
        });

        ConfigureAuthority(modelBuilder);
    }
}
