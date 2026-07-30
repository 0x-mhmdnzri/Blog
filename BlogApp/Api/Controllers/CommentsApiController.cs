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
[Route("api/v1/comments")]
[EnableRateLimiting("api")]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.Scheme)]
public class CommentsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<ApiCommentCreateDto> _validator;

    public CommentsApiController(ApplicationDbContext db, IValidator<ApiCommentCreateDto> validator)
    {
        _db = db;
        _validator = validator;
    }

    [HttpPost]
    public async Task<ActionResult<ApiCommentDto>> Create([FromBody] ApiCommentCreateDto dto)
    {
        if (!HasScope(ApiScopes.Write)) return Forbid();

        var result = await _validator.ValidateAsync(dto);
        if (!result.IsValid)
            return BadRequest(new ApiErrorDto("Validation failed", string.Join("; ", result.Errors.Select(e => e.ErrorMessage))));

        var postExists = await _db.Posts.AsNoTracking()
            .AnyAsync(p => p.Id == dto.PostId && !p.IsDeleted && p.IsPublished);
        if (!postExists) return NotFound(new ApiErrorDto("Post not found"));

        var comment = new Comment
        {
            PostId = dto.PostId,
            AuthorName = dto.AuthorName.Trim(),
            Body = dto.Body.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            IsApproved = false
        };

        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Create), new ApiCommentDto(
            comment.Id, comment.PostId, comment.AuthorName, comment.Body, comment.CreatedAtUtc, comment.LikeCount));
    }

    private bool HasScope(string scope)
    {
        var scopes = User.FindFirst("api_scopes")?.Value ?? "";
        return ApiScopes.Has(scopes, scope);
    }
}
