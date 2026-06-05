namespace Cargo.BuildingBlocks.Messaging;

/// <summary>
/// RabbitMQ connection settings shared by outbox publishers and the
/// notification service consumer.
/// Bind from config section "RabbitMq".
/// </summary>
public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public string Host        { get; init; } = "localhost";
    public int    Port        { get; init; } = 5672;
    public string Username    { get; init; } = "guest";
    public string Password    { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";

    /// <summary>Topic exchange that all notifications flow through.</summary>
    public string ExchangeName { get; init; } = "notifications";

    /// <summary>Durable queue bound to <c>notification.#</c> on the exchange.</summary>
    public string QueueName { get; init; } = "notification-service-queue";
}
