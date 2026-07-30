using System.Text.Json;
using BlogApp.Api.Auth;
using BlogApp.Api.Dtos;
using BlogApp.Models;
using BlogApp.Services.Messaging;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BlogApp.Api.Controllers;

[ApiController]
[Route("api/v1/comments")]
[EnableRateLimiting("api")]
[IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.Scheme)]
public class CommentsApiController : ControllerBase
{
    private readonly IValidator<ApiCommentCreateDto> _validator;
    private readonly IApiTopicBus _bus;

    public CommentsApiController(IValidator<ApiCommentCreateDto> validator, IApiTopicBus bus)
    {
        _validator = validator;
        _bus = bus;
    }

    /// <summary>Create comment — durable topic enqueue, sequential worker, no drop.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ApiCommentCreateDto dto, CancellationToken ct)
    {
        if (!HasScope(ApiScopes.Write)) return Forbid();

        var result = await _validator.ValidateAsync(dto, ct);
        if (!result.IsValid)
            return BadRequest(new ApiErrorDto("Validation failed", string.Join("; ", result.Errors.Select(e => e.ErrorMessage))));

        int? keyId = null;
        if (int.TryParse(User.FindFirst("api_key_id")?.Value, out var kid)) keyId = kid;

        var work = new ApiWorkRequest
        {
            Kind = "comments.create",
            Method = "POST",
            Path = "/api/v1/comments",
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            ApiKeyId = keyId,
            PayloadJson = JsonSerializer.Serialize(dto)
        };

        var res = await _bus.EnqueueAndWaitAsync(work, ct: ct);
        if (!res.Ok)
            return StatusCode(res.StatusCode, new ApiErrorDto(res.Error ?? "work_failed"));

        return new ContentResult
        {
            StatusCode = res.StatusCode,
            ContentType = "application/json",
            Content = res.BodyJson ?? "{}"
        };
    }

    private bool HasScope(string scope)
    {
        var scopes = User.FindFirst("api_scopes")?.Value ?? "";
        return ApiScopes.Has(scopes, scope);
    }
}
