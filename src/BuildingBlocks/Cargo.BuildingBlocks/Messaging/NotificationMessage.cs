using Cargo.BuildingBlocks.Notifications.Email;

namespace Cargo.BuildingBlocks.Messaging;

/// <summary>
/// The envelope that travels through RabbitMQ (serialised as JSON).
/// Use the static factory methods to construct well-typed messages.
/// The Payload dictionary carries channel-specific fields; the consumer
/// on the NotificationService side knows which keys to expect for each
/// Channel + Type combination.
/// </summary>
public sealed record NotificationMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public NotificationChannel Channel { get; init; }

    /// <summary>
    /// Human-readable sub-type within a channel, e.g. "EmailVerification".
    /// Drives template/handler selection on the consumer side.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Loosely-typed bag of channel-specific values.
    /// Kept intentionally minimal — only what the consumer needs to act.
    /// </summary>
    public Dictionary<string, string> Payload { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // ── Email factory methods ───────────────────────────────────────────

    /// <summary>Creates an OTP email notification message.</summary>
    public static NotificationMessage EmailOtp(
        string toEmail,
        string toName,
        string otp,
        OtpEmailType emailType)
        => new()
        {
            Channel = NotificationChannel.Email,
            Type    = emailType.ToString(),
            Payload = new()
            {
                ["ToEmail"] = toEmail,
                ["ToName"]  = toName,
                ["Otp"]     = otp
            }
        };

    // ── WhatsApp factory methods ────────────────────────────────────────

    /// <summary>Creates a WhatsApp text notification message.</summary>
    public static NotificationMessage WhatsApp(string phoneNumber, string message)
        => new()
        {
            Channel = NotificationChannel.WhatsApp,
            Type    = "WhatsAppMessage",
            Payload = new()
            {
                ["PhoneNumber"] = phoneNumber,
                ["Message"]     = message
            }
        };

    // ── Mobile push factory (future) ────────────────────────────────────

    /// <summary>
    /// Creates a generic mobile push notification.
    /// The FCM/APNs handler is a stub until the mobile apps are built.
    /// </summary>
    public static NotificationMessage Push(
        string deviceToken, string title, string body)
        => new()
        {
            Channel = NotificationChannel.Push,
            Type    = "GenericPush",
            Payload = new()
            {
                ["DeviceToken"] = deviceToken,
                ["Title"]       = title,
                ["Body"]        = body
            }
        };
}
