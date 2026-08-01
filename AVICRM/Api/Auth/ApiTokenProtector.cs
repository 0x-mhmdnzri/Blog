using Microsoft.AspNetCore.DataProtection;

namespace AVICRM.Api.Auth;

/// <summary>
/// Encrypts PAT plaintext with ASP.NET Data Protection so the owner can copy later.
/// Auth still uses KeyHash only — ciphertext is never used for verification.
/// </summary>
public interface IApiTokenProtector
{
    string Protect(string plainToken);
    string? Unprotect(string? protectedPayload);
}

public sealed class ApiTokenProtector : IApiTokenProtector
{
    private readonly IDataProtector _protector;

    public ApiTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("AVICRM.ApiKeys.Token.v1");
    }

    public string Protect(string plainToken) => _protector.Protect(plainToken);

    public string? Unprotect(string? protectedPayload)
    {
        if (string.IsNullOrEmpty(protectedPayload)) return null;
        try { return _protector.Unprotect(protectedPayload); }
        catch { return null; }
    }
}
