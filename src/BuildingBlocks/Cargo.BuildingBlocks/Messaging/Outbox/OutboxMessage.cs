namespace Cargo.BuildingBlocks.Messaging.Outbox;

/// <summary>
/// A pending notification stored in the service's own database.
/// Written transactionally alongside the triggering domain event, then
/// picked up and forwarded to RabbitMQ by <see cref="OutboxPublisherWorker"/>.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>JSON-serialised <see cref="NotificationMessage"/>.</summary>
    public string Payload { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Set by the worker once the message is successfully published to RabbitMQ.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Last error message if publishing failed; null on success.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Number of failed publish attempts.
    /// The worker stops retrying after 5 attempts to avoid poison-pill loops.
    /// </summary>
    public int RetryCount { get; set; }
}
