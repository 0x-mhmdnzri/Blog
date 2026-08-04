using System.Net;
using System.Net.Mail;
using BlogApp.Models;
using Microsoft.Extensions.Options;

namespace BlogApp.Services.Messaging;

/// <summary>
/// Legacy options type kept for optional one-time bootstrap only.
/// Runtime values come from SiteSettings (DB) via ISiteConfigService.
/// </summary>
public class SmtpOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "noreply@localhost";
    public string FromDisplayName { get; set; } = "Blog";
}

/// <summary>
/// Sends mail using SMTP settings stored in the database (SuperAdmin → /AdminSettings).
/// Not bound to appsettings.json / .env at runtime.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IServiceScopeFactory scopeFactory, ILogger<SmtpEmailSender> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, bool isHtml = true, CancellationToken ct = default)
    {
        var opt = await LoadFromDbAsync(ct);

        if (!opt.Enabled || string.IsNullOrWhiteSpace(opt.Host))
        {
            _logger.LogDebug("SMTP disabled — skip email To={To} Subject={Subject}", to, subject);
            return;
        }

        using var msg = new MailMessage
        {
            From = new MailAddress(
                string.IsNullOrWhiteSpace(opt.FromAddress) ? "noreply@localhost" : opt.FromAddress,
                string.IsNullOrWhiteSpace(opt.FromDisplayName) ? "Blog" : opt.FromDisplayName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };
        msg.To.Add(to);

        using var client = new SmtpClient(opt.Host, opt.Port)
        {
            EnableSsl = opt.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrEmpty(opt.UserName))
            client.Credentials = new NetworkCredential(opt.UserName, opt.Password);

        try
        {
            await client.SendMailAsync(msg, ct);
            _logger.LogInformation("Email sent To={To} Subject={Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email failed To={To} Subject={Subject}", to, subject);
            throw;
        }
    }

    private async Task<SmtpOptions> LoadFromDbAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<ISiteConfigService>();

        var portRaw = await cfg.GetAsync(SiteSettingKeys.SmtpPort, ct);
        if (!int.TryParse(portRaw, out var port) || port <= 0) port = 587;

        return new SmtpOptions
        {
            Enabled = await cfg.GetBoolAsync(SiteSettingKeys.SmtpEnabled, false, ct),
            Host = (await cfg.GetAsync(SiteSettingKeys.SmtpHost, ct))?.Trim() ?? "",
            Port = port,
            EnableSsl = await cfg.GetBoolAsync(SiteSettingKeys.SmtpEnableSsl, true, ct),
            UserName = (await cfg.GetAsync(SiteSettingKeys.SmtpUserName, ct)) ?? "",
            Password = (await cfg.GetAsync(SiteSettingKeys.SmtpPassword, ct)) ?? "",
            FromAddress = (await cfg.GetAsync(SiteSettingKeys.SmtpFromAddress, ct))?.Trim() ?? "noreply@localhost",
            FromDisplayName = (await cfg.GetAsync(SiteSettingKeys.SmtpFromDisplayName, ct))?.Trim() ?? "Blog"
        };
    }
}

public class SmsOptions
{
    public bool Enabled { get; set; }
    /// <summary>Provider name for your integration (twilio, kavenegar, …).</summary>
    public string Provider { get; set; } = "none";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string FromNumber { get; set; } = "";
    public string Endpoint { get; set; } = "";
}

/// <summary>Stub: logs only. Replace body with your SMS HTTP call when ready.</summary>
public sealed class ConfigurableSmsSender : ISmsSender
{
    private readonly SmsOptions _opt;
    private readonly ILogger<ConfigurableSmsSender> _logger;

    public ConfigurableSmsSender(IOptions<SmsOptions> opt, ILogger<ConfigurableSmsSender> logger)
    {
        _opt = opt.Value;
        _logger = logger;
    }

    public Task SendAsync(string phoneE164, string message, CancellationToken ct = default)
    {
        if (!_opt.Enabled)
        {
            _logger.LogDebug("SMS disabled — skip To={Phone}", phoneE164);
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "SMS queued Provider={Provider} To={Phone} Len={Len} (wire your provider in ConfigurableSmsSender)",
            _opt.Provider, phoneE164, message.Length);
        return Task.CompletedTask;
    }
}

public class PushOptions
{
    public bool Enabled { get; set; }
    public string VapidPublicKey { get; set; } = "";
    public string VapidPrivateKey { get; set; } = "";
    public string Subject { get; set; } = "mailto:admin@localhost";
}

public sealed class NoOpPushSender : IPushSender
{
    private readonly PushOptions _opt;
    private readonly ILogger<NoOpPushSender> _logger;

    public NoOpPushSender(IOptions<PushOptions> opt, ILogger<NoOpPushSender> logger)
    {
        _opt = opt.Value;
        _logger = logger;
    }

    public Task SendAsync(string userId, string title, string body, string? url = null, CancellationToken ct = default)
    {
        if (!_opt.Enabled)
        {
            _logger.LogDebug("Push disabled — skip UserId={UserId}", userId);
            return Task.CompletedTask;
        }

        _logger.LogInformation("Push queued UserId={UserId} Title={Title} (add WebPush package + subscriptions when ready)",
            userId, title);
        return Task.CompletedTask;
    }
}
