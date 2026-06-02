using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Cargo.BuildingBlocks.Messaging.Outbox;

/// <summary>
/// Implements <see cref="INotificationPublisher"/> by writing the message to
/// the outbox table in the calling service's database.
/// A separate <see cref="OutboxPublisherWorker"/> polls the table and forwards
/// persisted messages to RabbitMQ, so notification delivery survives transient
/// broker outages.
///
/// Failure semantics:
///   If the outbox write itself fails we log a warning but do NOT re-throw.
///   Notification delivery is treated as a non-critical side-effect — the same
///   policy that existed before (email exceptions were silently caught).
/// </summary>
public sealed class OutboxNotificationPublisher(
    IOutboxDbContext dbContext,
    ILogger<OutboxNotificationPublisher> logger) : INotificationPublisher
{
    public async Task PublishAsync(NotificationMessage message, CancellationToken ct = default)
    {
        try
        {
            var outboxMessage = new OutboxMessage
            {
                Payload = JsonSerializer.Serialize(message)
            };

            dbContext.OutboxMessages.Add(outboxMessage);
            await dbContext.SaveChangesAsync(ct);

            logger.LogDebug(
                "Notification {MessageId} ({Channel}/{Type}) written to outbox.",
                message.Id, message.Channel, message.Type);
        }
        catch (Exception ex)
        {
            // Non-critical — matches the existing "email failure is non-fatal" contract.
            logger.LogWarning(ex,
                "Failed to write notification {MessageId} ({Channel}/{Type}) to outbox. " +
                "The notification will not be delivered.",
                message.Id, message.Channel, message.Type);
        }
    }
}
