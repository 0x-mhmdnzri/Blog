using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace AVICRM.Services.Messaging;

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

/// <summary>Uses classic SmtpClient so you only fill Smtp:* in appsettings / .env.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _opt;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> opt, ILogger<SmtpEmailSender> logger)
    {
        _opt = opt.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, bool isHtml = true, CancellationToken ct = default)
    {
        if (!_opt.Enabled || string.IsNullOrWhiteSpace(_opt.Host))
        {
            _logger.LogDebug("SMTP disabled — skip email To={To} Subject={Subject}", to, subject);
            return;
        }

        using var msg = new MailMessage
        {
            From = new MailAddress(_opt.FromAddress, _opt.FromDisplayName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };
        msg.To.Add(to);

        using var client = new SmtpClient(_opt.Host, _opt.Port)
        {
            EnableSsl = _opt.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrEmpty(_opt.UserName))
            client.Credentials = new NetworkCredential(_opt.UserName, _opt.Password);

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

        // Hook: call _opt.Endpoint with ApiKey/Secret/FromNumber using HttpClient in your deployment.
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
