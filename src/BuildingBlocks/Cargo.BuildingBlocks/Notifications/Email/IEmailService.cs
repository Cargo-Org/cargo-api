namespace Cargo.BuildingBlocks.Notifications.Email;

/// <summary>
/// Sends transactional emails via SMTP (Gmail in dev/prod).
/// The implementation lives in the Infrastructure layer using MailKit.
/// </summary>
public interface IEmailService
{
    /// <summary>Sends a single email message.</summary>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);

    /// <summary>
    /// Convenience method: renders and sends the standard OTP email template.
    /// </summary>
    Task SendOtpAsync(string toEmail, string toName, string otp,
                      OtpEmailType emailType, CancellationToken ct = default);
}

/// <summary>Represents a single outbound email.</summary>
public sealed record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null
);

/// <summary>Controls which subject / copy the OTP email uses.</summary>
public enum OtpEmailType
{
    EmailVerification,
    PasswordReset
}