namespace Cargo.BuildingBlocks.Messaging;

/// <summary>
/// Abstracts notification dispatch for callers.
/// The production implementation (<see cref="Outbox.OutboxNotificationPublisher"/>)
/// writes the message to an outbox table in the caller's own database;
/// a background worker then forwards it to RabbitMQ, providing durable
/// at-least-once delivery even when the broker is temporarily unavailable.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Enqueues a notification for async delivery.
    /// The call returns as soon as the message is written to the outbox —
    /// actual delivery is handled by <see cref="Outbox.OutboxPublisherWorker"/>.
    /// Failures are logged but never re-thrown; notification delivery is
    /// always treated as a non-critical side-effect.
    /// </summary>
    Task PublishAsync(NotificationMessage message, CancellationToken ct = default);
}
