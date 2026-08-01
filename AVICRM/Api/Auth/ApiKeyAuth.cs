using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using AVICRM.Data;
using AVICRM.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AVICRM.Api.Auth;

public static class ApiKeyDefaults
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string TokenPrefix = "blog_";
}

public sealed class ApiKeyAuthOptions : AuthenticationSchemeOptions
{
}

public sealed class ApiKeyAuthHandler : AuthenticationHandler<ApiKeyAuthOptions>
{
    private readonly ApplicationDbContext _db;

    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApplicationDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? raw = null;
        if (Request.Headers.TryGetValue(ApiKeyDefaults.HeaderName, out var h))
            raw = h.FirstOrDefault();
        else if (Request.Headers.TryGetValue("Authorization", out var auth))
        {
            var v = auth.FirstOrDefault();
            if (!string.IsNullOrEmpty(v) && v.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                raw = v["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(raw))
            return AuthenticateResult.NoResult();

        if (!raw.StartsWith(ApiKeyDefaults.TokenPrefix, StringComparison.Ordinal))
            return AuthenticateResult.Fail("Invalid API key format.");

        var hash = ApiKeyHasher.Hash(raw);
        var key = await _db.ApiKeys.AsNoTracking()
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyHash == hash);

        if (key is null)
            return AuthenticateResult.Fail("API key not found.");

        if (key.IsBanned)
            return AuthenticateResult.Fail("API key is banned.");

        if (key.ApprovalStatus != ApiKeyApprovalStatus.Approved)
            return AuthenticateResult.Fail("API key is pending SuperAdmin approval.");

        if (!key.IsActive)
            return AuthenticateResult.Fail("API key is disabled.");

        if (key.ExpiresAtUtc is { } exp && exp < DateTime.UtcNow)
            return AuthenticateResult.Fail("API key expired.");

        await _db.ApiKeys
            .Where(k => k.Id == key.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(k => k.LastUsedAtUtc, DateTime.UtcNow)
                .SetProperty(k => k.RequestCount, k => k.RequestCount + 1));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, key.UserId),
            new(ClaimTypes.Name, key.User?.UserName ?? key.UserId),
            new("api_key_id", key.Id.ToString()),
            new("api_scopes", key.Scopes),
            new("auth_method", "api_key")
        };

        var identity = new ClaimsIdentity(claims, ApiKeyDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }
}

public static class ApiKeyHasher
{
    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static (string Token, string Prefix, string Hash) Generate()
    {
        var random = RandomNumberGenerator.GetBytes(32);
        var token = ApiKeyDefaults.TokenPrefix + Convert.ToHexString(random).ToLowerInvariant();
        var prefix = token[..12];
        return (token, prefix, Hash(token));
    }
}

public static class ApiKeyAbuseService
{
    public const int StrikeThreshold = 5;

    public static async Task RegisterRateLimitStrikeAsync(ApplicationDbContext db, int apiKeyId, string reason)
    {
        var key = await db.ApiKeys.AsTracking().FirstOrDefaultAsync(k => k.Id == apiKeyId);
        if (key is null || key.IsBanned) return;

        key.AbuseStrikeCount++;
        key.LastAbuseAtUtc = DateTime.UtcNow;

        if (key.AbuseStrikeCount >= StrikeThreshold)
        {
            key.IsBanned = true;
            key.IsActive = false;
            key.BannedAtUtc = DateTime.UtcNow;
            key.BanReason = $"Auto-ban: {reason} (strikes={key.AbuseStrikeCount})";
        }

        await db.SaveChangesAsync();
    }
}
