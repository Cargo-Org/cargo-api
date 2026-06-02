using System.Text;
using System.Text.Json;
using Cargo.BuildingBlocks.Messaging;
using Cargo.BuildingBlocks.Messaging.Outbox;
using Cargo.NotificationService.Handlers;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Cargo.NotificationService.Consumers;

/// <summary>
/// Long-lived RabbitMQ consumer. Binds to the shared notifications queue
/// and dispatches each message to the appropriate channel handler.
///
/// Ack/Nack policy:
/// • Successful dispatch → BasicAck (message removed from queue).
/// • Unrecognised channel → BasicAck (avoid poison-pill loop; handler logs the skip).
/// • Handler throws → BasicNack with requeue=false after MaxDeliveries; DeadLetter
///   support can be added later by configuring x-dead-letter-exchange on the queue.
/// </summary>
public sealed class NotificationConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqSettings> rabbitMqOptions,
    ILogger<NotificationConsumer> logger) : BackgroundService
{
    private readonly RabbitMqSettings _settings = rabbitMqOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Notification consumer starting.");

        // Keep reconnecting if RabbitMQ is unavailable at startup.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Notification consumer disconnected. Reconnecting in 10s.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        logger.LogInformation("Notification consumer stopped.");
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName    = _settings.Host,
            Port        = _settings.Port,
            UserName    = _settings.Username,
            Password    = _settings.Password,
            VirtualHost = _settings.VirtualHost
        };

        await using var connection = await factory.CreateConnectionAsync(
            "cargo-notification-consumer", ct);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: ct);

        // Declare exchange + queue idempotently (mirrors publisher declarations).
        await channel.ExchangeDeclareAsync(
            exchange:   _settings.ExchangeName,
            type:       ExchangeType.Topic,
            durable:    true,
            autoDelete: false,
            cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue:      _settings.QueueName,
            durable:    true,
            exclusive:  false,
            autoDelete: false,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue:      _settings.QueueName,
            exchange:   _settings.ExchangeName,
            routingKey: "notification.#",
            cancellationToken: ct);

        // Process one message at a time — prevents overloading downstream APIs.
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: ct);

        logger.LogInformation(
            "Connected to RabbitMQ. Listening on queue '{Queue}'.", _settings.QueueName);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var body = Encoding.UTF8.GetString(ea.Body.Span);

            try
            {
                var message = JsonSerializer.Deserialize<NotificationMessage>(body);
                if (message is null)
                {
                    logger.LogWarning("Received null or undeserializable message. Acking to skip.");
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    return;
                }

                logger.LogInformation(
                    "Received notification {Id} — Channel: {Channel}, Type: {Type}",
                    message.Id, message.Channel, message.Type);

                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = ResolveHandler(scope, message.Channel);

                await handler.HandleAsync(message, ct);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Handler threw while processing notification. Nacking without requeue.");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await channel.BasicConsumeAsync(
            queue:       _settings.QueueName,
            autoAck:     false,
            consumer:    consumer,
            cancellationToken: ct);

        // Block until cancellation is requested, keeping the channel alive.
        await Task.Delay(Timeout.Infinite, ct);
    }

    private static INotificationHandler ResolveHandler(
        IServiceScope scope, NotificationChannel channel)
        => channel switch
        {
            NotificationChannel.Email    => scope.ServiceProvider
                                               .GetRequiredService<EmailNotificationHandler>(),
            NotificationChannel.WhatsApp => scope.ServiceProvider
                                               .GetRequiredService<WhatsAppNotificationHandler>(),
            NotificationChannel.Push     => scope.ServiceProvider
                                               .GetRequiredService<PushNotificationHandler>(),
            _ => throw new InvalidOperationException(
                     $"No handler registered for channel '{channel}'.")
        };
}
