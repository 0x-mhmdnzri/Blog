using AVICRM.Data;
using AVICRM.Models;
using AVICRM.Models.ViewModels;
using AVICRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminAuditController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminAuditController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "گزارش حسابرسی";
        var since = DateTime.UtcNow.AddDays(-30);
        ViewBag.TopActions = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.CreatedAtUtc >= since)
            .GroupBy(a => a.Action)
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Data()
    {
        var req = DataTablesRequest.From(Request);
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        var total = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(req.SearchValue))
        {
            var term = req.SearchValue;
            query = query.Where(a =>
                a.Action.Contains(term)
                || (a.ActorUserName != null && a.ActorUserName.Contains(term))
                || (a.EntityType != null && a.EntityType.Contains(term))
                || (a.EntityId != null && a.EntityId.Contains(term))
                || (a.Details != null && a.Details.Contains(term))
                || (a.IpAddress != null && a.IpAddress.Contains(term)));
        }

        var filtered = await query.CountAsync();

        // 0 #, 1 time, 2 user, 3 action, 4 entity, 5 details, 6 ip
        query = (req.OrderColumn, req.Asc) switch
        {
            (1, true) => query.OrderBy(a => a.CreatedAtUtc),
            (1, false) => query.OrderByDescending(a => a.CreatedAtUtc),
            (2, true) => query.OrderBy(a => a.ActorUserName),
            (2, false) => query.OrderByDescending(a => a.ActorUserName),
            (3, true) => query.OrderBy(a => a.Action),
            (3, false) => query.OrderByDescending(a => a.Action),
            (4, true) => query.OrderBy(a => a.EntityType),
            (4, false) => query.OrderByDescending(a => a.EntityType),
            (6, true) => query.OrderBy(a => a.IpAddress),
            (6, false) => query.OrderByDescending(a => a.IpAddress),
            _ => query.OrderByDescending(a => a.CreatedAtUtc)
        };

        var page = await query.Skip(req.Start).Take(req.Length).ToListAsync();
        var rows = page.Select((a, i) => new object[]
        {
            req.Start + i + 1,
            PersianDate.DateTime(a.CreatedAtUtc),
            System.Net.WebUtility.HtmlEncode(a.ActorUserName ?? "—"),
            System.Net.WebUtility.HtmlEncode(a.Action),
            System.Net.WebUtility.HtmlEncode((a.EntityType ?? "") + (string.IsNullOrEmpty(a.EntityId) ? "" : "#" + a.EntityId)),
            System.Net.WebUtility.HtmlEncode(a.Details ?? ""),
            System.Net.WebUtility.HtmlEncode(a.IpAddress ?? "")
        }).ToList();

        return Json(DataTablesResponse.Ok(req.Draw, total, filtered, rows));
    }

    /// <summary>Export ALL audit logs matching optional search (no page limit).</summary>
    [HttpGet]
    public async Task<IActionResult> ExportCsv(string? search = null)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a =>
                a.Action.Contains(term)
                || (a.ActorUserName != null && a.ActorUserName.Contains(term))
                || (a.EntityType != null && a.EntityType.Contains(term))
                || (a.EntityId != null && a.EntityId.Contains(term))
                || (a.Details != null && a.Details.Contains(term))
                || (a.IpAddress != null && a.IpAddress.Contains(term)));
        }

        var list = await query.OrderByDescending(a => a.CreatedAtUtc).ToListAsync();

        var headers = new[]
        {
            "Id", "CreatedAtUtc", "ActorUserName", "Action", "EntityType", "EntityId", "Details", "IpAddress"
        };

        var rows = list.Select(a => new[]
        {
            CsvExport.Cell(a.Id),
            CsvExport.Cell(a.CreatedAtUtc),
            CsvExport.Cell(a.ActorUserName),
            CsvExport.Cell(a.Action),
            CsvExport.Cell(a.EntityType),
            CsvExport.Cell(a.EntityId),
            CsvExport.Cell(a.Details),
            CsvExport.Cell(a.IpAddress)
        });

        return CsvExport.File($"audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv", headers, rows);
    }
}
