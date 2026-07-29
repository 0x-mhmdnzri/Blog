namespace BlogApp.Services.Messaging;

/// <summary>
/// Plug in your own SMTP (or SendGrid/Mailgun) via configuration.
/// Default implementation uses System.Net.Mail SmtpClient from appsettings Smtp:* .
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, bool isHtml = true, CancellationToken ct = default);
}

public interface ISmsSender
{
    /// <summary>Send SMS via your provider (Twilio, Kavenegar, etc.). No-op until configured.</summary>
    Task SendAsync(string phoneE164, string message, CancellationToken ct = default);
}

public interface IPushSender
{
    /// <summary>Web-push / FCM — optional; no-op until VAPID/keys configured.</summary>
    Task SendAsync(string userId, string title, string body, string? url = null, CancellationToken ct = default);
}
