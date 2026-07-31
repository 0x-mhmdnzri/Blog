using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public partial class ApplicationDbContext
{
    public DbSet<CustomTheme> CustomThemes => Set<CustomTheme>();

    partial void OnModelCreatingThemes(ModelBuilder modelBuilder);
}
