using AVICRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Data;

/// <summary>
/// Push + webhook delivery DbSets (partial of ApplicationDbContext).
/// Keeps notification infrastructure compile-complete without rewriting the main context file.
/// </summary>
public partial class ApplicationDbContext
{
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
}
