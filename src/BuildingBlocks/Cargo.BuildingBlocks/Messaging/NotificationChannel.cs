namespace Cargo.BuildingBlocks.Messaging;

/// <summary>
/// Identifies the delivery channel for a <see cref="NotificationMessage"/>.
/// The outbox worker embeds this value in the RabbitMQ routing key
/// so the notification consumer can dispatch to the correct handler.
/// </summary>
public enum NotificationChannel
{
    Email,
    WhatsApp,
    Push
}
