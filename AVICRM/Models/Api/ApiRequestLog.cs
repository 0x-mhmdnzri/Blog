using System.ComponentModel.DataAnnotations;

namespace AVICRM.Models;

public class ApiRequestLog
{
    public long Id { get; set; }
    public int? ApiKeyId { get; set; }
    public ApiKey? ApiKey { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    [MaxLength(80)]
    public string? UserName { get; set; }

    [MaxLength(16)]
    public string? KeyPrefix { get; set; }

    [Required, MaxLength(10)]
    public string Method { get; set; } = "GET";

    [Required, MaxLength(400)]
    public string Path { get; set; } = "/";

    [MaxLength(200)]
    public string? Query { get; set; }

    public int StatusCode { get; set; }
    public int DurationMs { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    [MaxLength(200)]
    public string? UserAgent { get; set; }

    public bool IsError { get; set; }
    public bool IsRateLimited { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
