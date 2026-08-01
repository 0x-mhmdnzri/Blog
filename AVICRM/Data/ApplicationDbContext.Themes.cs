using AVICRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

public partial class ApplicationDbContext
{
    public DbSet<CustomTheme> CustomThemes => Set<CustomTheme>();

    partial void OnModelCreatingThemes(ModelBuilder modelBuilder);
}
