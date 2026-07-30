using BlogApp.Api.Auth;
using BlogApp.Api.Dtos;
using BlogApp.Data;
using BlogApp.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
[EnableRateLimiting("api")]
[IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.Scheme)]
public class WebhooksApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<ApiWebhookCreateDto> _validator;

    public WebhooksApiController(ApplicationDbContext db, IValidator<ApiWebhookCreateDto> validator)
    {
        _db = db;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ApiWebhookDto>>> List()
    {
        if (!HasScope(ApiScopes.Webhooks) && !HasScope(ApiScopes.Read)) return Forbid();
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Unauthorized();

        var items = await _db.WebhookSubscriptions.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.Id)
            .Select(w => new ApiWebhookDto(w.Id, w.TargetUrl, w.Events, w.IsActive, w.CreatedAtUtc))
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<ApiWebhookDto>> Create([FromBody] ApiWebhookCreateDto dto)
    {
        if (!HasScope(ApiScopes.Webhooks)) return Forbid();

        var result = await _validator.ValidateAsync(dto);
        if (!result.IsValid)
            return BadRequest(new ApiErrorDto("Validation failed", string.Join("; ", result.Errors.Select(e => e.ErrorMessage))));

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Unauthorized();

        var keyId = int.TryParse(User.FindFirst("api_key_id")?.Value, out var kid) ? kid : (int?)null;

        var sub = new WebhookSubscription
        {
            UserId = userId,
            ApiKeyId = keyId,
            TargetUrl = dto.TargetUrl.Trim(),
            Secret = string.IsNullOrWhiteSpace(dto.Secret) ? Guid.NewGuid().ToString("N") : dto.Secret.Trim(),
            Events = string.IsNullOrWhiteSpace(dto.Events) ? "post.published" : dto.Events.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        _db.WebhookSubscriptions.Add(sub);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(List), new ApiWebhookDto(sub.Id, sub.TargetUrl, sub.Events, sub.IsActive, sub.CreatedAtUtc));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!HasScope(ApiScopes.Webhooks)) return Forbid();
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Unauthorized();

        var sub = await _db.WebhookSubscriptions.AsTracking()
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);
        if (sub is null) return NotFound();

        _db.WebhookSubscriptions.Remove(sub);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private bool HasScope(string scope)
    {
        var scopes = User.FindFirst("api_scopes")?.Value ?? "";
        return ApiScopes.Has(scopes, scope);
    }
}
