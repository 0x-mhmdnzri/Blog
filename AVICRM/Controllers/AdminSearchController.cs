using AVICRM.Models;
using AVICRM.Services.AdminSearch;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AVICRM.Controllers;

[Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
[Route("AdminSearch")]
public sealed class AdminSearchController : Controller
{
    private readonly AdminSearchService _search;

    public AdminSearchController(AdminSearchService search) => _search = search;

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Search";
        return View();
    }

    [HttpGet("api")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Api(
        [FromQuery] string? q,
        [FromQuery] string? scope,
        [FromQuery] int take = 24,
        [FromQuery] int skip = 0,
        CancellationToken ct = default)
    {
        var result = await _search.SearchAsync(new AdminSearchRequest
        {
            Q = q ?? string.Empty,
            Scope = scope ?? "all",
            Take = take,
            Skip = skip
        }, ct);
        return Json(result);
    }

    [HttpPost("reindex")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reindex(CancellationToken ct)
    {
        await _search.RebuildIndexAsync(ct);
        TempData["Ok"] = "Search index rebuilt.";
        return RedirectToAction(nameof(Index));
    }
}
