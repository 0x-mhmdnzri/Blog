using AVICRM.Api.Dtos;
using FluentValidation;

namespace AVICRM.Api.Validation;

/// <summary>FluentValidation rules — free OSS package; sanitize lengths, charset, SSRF, injection patterns.</summary>
public sealed class ApiCommentCreateValidator : AbstractValidator<ApiCommentCreateDto>
{
    public ApiCommentCreateValidator()
    {
        RuleFor(x => x.PostId).GreaterThan(0);
        RuleFor(x => x.AuthorName)
            .NotEmpty().MaximumLength(80)
            .Must(InputSanitizer.IsSafePlainText).WithMessage("Author name contains invalid characters.");
        RuleFor(x => x.Body)
            .NotEmpty().MaximumLength(2000)
            .Must(InputSanitizer.IsSafePlainText).WithMessage("Body contains disallowed content.");
    }
}

public sealed class ApiWebhookCreateValidator : AbstractValidator<ApiWebhookCreateDto>
{
    public ApiWebhookCreateValidator()
    {
        RuleFor(x => x.TargetUrl)
            .NotEmpty().MaximumLength(500)
            .Must(InputSanitizer.IsHttpsUrl).WithMessage("Webhook URL must be https.")
            .Must(InputSanitizer.IsNotPrivateHost).WithMessage("Webhook URL host is not allowed.");
        RuleFor(x => x.Secret).MaximumLength(120).When(x => x.Secret != null);
        RuleFor(x => x.Events).MaximumLength(300).When(x => x.Events != null);
    }
}

public sealed class ApiKeyCreateValidator : AbstractValidator<ApiKeyCreateDto>
{
    public ApiKeyCreateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().MaximumLength(120)
            .Must(InputSanitizer.IsSafePlainText);
        RuleFor(x => x.Scopes)
            .MaximumLength(200)
            .Must(s => s == null || InputSanitizer.IsSafeScopeList(s))
            .WithMessage("Invalid scopes (allowed: read, write, webhooks).");
        RuleFor(x => x.ExpiresInDays)
            .InclusiveBetween(1, 3650).When(x => x.ExpiresInDays.HasValue);
    }
}

public sealed class ApiSearchQueryValidator : AbstractValidator<string>
{
    public ApiSearchQueryValidator()
    {
        RuleFor(x => x)
            .NotEmpty().MinimumLength(2).MaximumLength(100)
            .Must(InputSanitizer.IsSafePlainText);
    }
}

public static class InputSanitizer
{
    private static readonly HashSet<string> AllowedScopes = new(StringComparer.OrdinalIgnoreCase)
        { "read", "write", "webhooks", "*" };

    public static bool IsSafePlainText(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        // Block null bytes, HTML tags, script-ish, SQL comment probes
        if (s.Contains('\0')) return false;
        if (s.Contains('<') || s.Contains('>')) return false;
        if (s.Contains("javascript:", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("onerror=", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    public static bool IsSafeScopeList(string scopes)
    {
        var parts = scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 && parts.All(p => AllowedScopes.Contains(p));
    }

    public static bool IsHttpsUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        return u.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNotPrivateHost(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        var host = u.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return false;
        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return false;
        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            var bytes = ip.GetAddressBytes();
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                // 10/8, 172.16/12, 192.168/16, 127/8, 169.254/16
                if (bytes[0] == 10) return false;
                if (bytes[0] == 127) return false;
                if (bytes[0] == 192 && bytes[1] == 168) return false;
                if (bytes[0] == 169 && bytes[1] == 254) return false;
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
            }
        }
        return true;
    }

    public static string Clamp(string? s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max]);
}
