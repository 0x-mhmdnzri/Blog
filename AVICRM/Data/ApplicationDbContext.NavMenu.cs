using AVICRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

public partial class ApplicationDbContext
{
    public DbSet<NavMenuItem> NavMenuItems => Set<NavMenuItem>();
}
