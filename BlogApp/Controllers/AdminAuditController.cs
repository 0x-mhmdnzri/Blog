using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminAuditController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminAuditController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(string? actionFilter = null, int page = 1)
    {
        const int pageSize = 40;
        if (page < 1) page = 1;

        ViewData["Title"] = "گزارش حسابرسی";
        ViewBag.ActionFilter = actionFilter;

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(actionFilter))
            query = query.Where(a => a.Action.Contains(actionFilter));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogItem
            {
                Id = a.Id,
                ActorUserName = a.ActorUserName,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Details = a.Details,
                IpAddress = a.IpAddress,
                CreatedAtUtc = a.CreatedAtUtc
            })
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        ViewBag.Total = total;

        // Simple action breakdown for dashboard strip
        var since = DateTime.UtcNow.AddDays(-30);
        ViewBag.TopActions = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.CreatedAtUtc >= since)
            .GroupBy(a => a.Action)
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync();

        return View(items);
    }
}
