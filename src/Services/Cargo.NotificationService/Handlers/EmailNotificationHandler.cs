using Cargo.BuildingBlocks.Messaging;
using Cargo.BuildingBlocks.Notifications.Email;
using Cargo.NotificationService.Notifications.Email;

namespace Cargo.NotificationService.Handlers;

/// <summary>
/// Handles NotificationChannel.Email messages by rendering the appropriate
/// OTP template and sending it via SMTP through MailKit.
/// </summary>
public sealed class EmailNotificationHandler(
    IEmailService emailService,
    ILogger<EmailNotificationHandler> logger) : INotificationHandler
{
    public async Task HandleAsync(NotificationMessage message, CancellationToken ct)
    {
        if (!message.Payload.TryGetValue("ToEmail", out var toEmail) ||
            !message.Payload.TryGetValue("ToName",  out var toName)  ||
            !message.Payload.TryGetValue("Otp",      out var otp))
        {
            logger.LogError(
                "Email message {Id} is missing required payload fields (ToEmail, ToName, Otp). Skipping.",
                message.Id);
            return;
        }

        if (!Enum.TryParse<OtpEmailType>(message.Type, ignoreCase: true, out var emailType))
        {
            logger.LogError(
                "Email message {Id} has unrecognised Type '{Type}'. Skipping.",
                message.Id, message.Type);
            return;
        }

        await emailService.SendOtpAsync(toEmail, toName, otp, emailType, ct);

        logger.LogInformation(
            "Email OTP ({Type}) delivered to {Email}.", message.Type, toEmail);
    }
}
