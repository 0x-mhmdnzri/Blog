namespace AVICRM.Api.Dtos;

public record ApiPostListItemDto(
    int Id,
    string Title,
    string Slug,
    string? Summary,
    string? Category,
    IReadOnlyList<string> Tags,
    DateTime? PublishedAtUtc,
    int ReadingTimeMinutes,
    string LanguageCode,
    string Url);

public record ApiPostDetailDto(
    int Id,
    string Title,
    string Slug,
    string? Summary,
    string ContentMarkdown,
    string? Category,
    IReadOnlyList<string> Tags,
    DateTime? PublishedAtUtc,
    int ReadingTimeMinutes,
    int ViewCount,
    int LikeCount,
    string LanguageCode,
    string? AuthorUserName,
    string Url);

public record ApiCommentCreateDto(int PostId, string AuthorName, string Body);

public record ApiCommentDto(int Id, int PostId, string AuthorName, string Body, DateTime CreatedAtUtc, int LikeCount);

public record ApiWebhookCreateDto(string TargetUrl, string? Secret, string? Events);

public record ApiWebhookDto(int Id, string TargetUrl, string Events, bool IsActive, DateTime CreatedAtUtc);

public record ApiKeyCreateDto(string Name, string? Scopes, int? ExpiresInDays);

public record ApiKeyCreatedDto(int Id, string Name, string KeyPrefix, string Token, string Scopes, DateTime? ExpiresAtUtc);

public record ApiKeyListItemDto(int Id, string Name, string KeyPrefix, string Scopes, bool IsActive, bool IsBanned, long RequestCount, int AbuseStrikeCount, DateTime CreatedAtUtc, DateTime? LastUsedAtUtc, DateTime? ExpiresAtUtc);

public record ApiErrorDto(string Error, string? Detail = null);

public record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
