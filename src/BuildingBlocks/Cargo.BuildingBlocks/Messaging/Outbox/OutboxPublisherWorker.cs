using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Cargo.BuildingBlocks.Messaging.Outbox;

/// <summary>
/// Background worker that polls the outbox table for unprocessed messages
/// and publishes them to the RabbitMQ notifications exchange.
///
/// Design decisions:
/// • Uses IServiceScopeFactory to obtain a scoped IOutboxDbContext per poll,
///   preventing long-lived EF Core tracking issues.
/// • Retries up to 5 times per message; beyond that the message is abandoned
///   and its Error column is updated for manual inspection.
/// • Polls every 15 seconds — acceptable for OTP delivery latency.
/// • A single long-lived AMQP connection is reused; it is re-created if it
///   drops (e.g. after a RabbitMQ restart).
/// </summary>
public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqSettings> rabbitMqOptions,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    private const int MaxRetries       = 5;
    private const int BatchSize        = 50;
    private const int PollIntervalSecs = 15;

    private readonly RabbitMqSettings _settings = rabbitMqOptions.Value;
    private IConnection? _connection;
    private IChannel?    _channel;

    // ── BackgroundService entry point ─────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox publisher worker started (poll every {Interval}s).",
            PollIntervalSecs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureRabbitMqConnectedAsync(stoppingToken);
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown — exit the loop.
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Outbox worker error. Will retry in {Interval}s.", PollIntervalSecs);
            }

            await Task.Delay(TimeSpan.FromSeconds(PollIntervalSecs), stoppingToken);
        }

        logger.LogInformation("Outbox publisher worker stopping.");
    }

    // ── RabbitMQ connection management ────────────────────────────────────

    private async Task EnsureRabbitMqConnectedAsync(CancellationToken ct)
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true)
            return;

        // Close stale resources before recreating.
        if (_channel is not null) { await _channel.DisposeAsync(); _channel = null; }
        if (_connection is not null) { await _connection.DisposeAsync(); _connection = null; }

        logger.LogInformation("Connecting outbox worker to RabbitMQ at {Host}:{Port}.",
            _settings.Host, _settings.Port);

        var factory = new ConnectionFactory
        {
            HostName    = _settings.Host,
            Port        = _settings.Port,
            UserName    = _settings.Username,
            Password    = _settings.Password,
            VirtualHost = _settings.VirtualHost
        };

        _connection = await factory.CreateConnectionAsync("cargo-outbox-publisher", ct);
        _channel    = await _connection.CreateChannelAsync(cancellationToken: ct);

        // Declare exchange and queue idempotently — safe to call on every reconnect.
        await _channel.ExchangeDeclareAsync(
            exchange:   _settings.ExchangeName,
            type:       ExchangeType.Topic,
            durable:    true,
            autoDelete: false,
            cancellationToken: ct);

        await _channel.QueueDeclareAsync(
            queue:      _settings.QueueName,
            durable:    true,
            exclusive:  false,
            autoDelete: false,
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue:      _settings.QueueName,
            exchange:   _settings.ExchangeName,
            routingKey: "notification.#",
            cancellationToken: ct);

        logger.LogInformation("Outbox worker connected to RabbitMQ.");
    }

    // ── Outbox poll & publish ─────────────────────────────────────────────

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IOutboxDbContext>();

        var pending = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < MaxRetries)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        logger.LogInformation("Processing {Count} pending outbox messages.", pending.Count);

        foreach (var outbox in pending)
        {
            try
            {
                var message    = JsonSerializer.Deserialize<NotificationMessage>(outbox.Payload)!;
                var routingKey = ToRoutingKey(message.Channel);
                var body       = Encoding.UTF8.GetBytes(outbox.Payload);

                var props = new BasicProperties
                {
                    Persistent  = true,
                    MessageId   = message.Id,
                    ContentType = "application/json"
                };

                await _channel!.BasicPublishAsync(
                    exchange:        _settings.ExchangeName,
                    routingKey:      routingKey,
                    mandatory:       false,
                    basicProperties: props,
                    body:            body,
                    cancellationToken: ct);

                outbox.ProcessedAt = DateTimeOffset.UtcNow;
                outbox.Error       = null;

                logger.LogInformation(
                    "Published outbox message {Id} ({Channel}) via routing key '{Key}'.",
                    outbox.Id, message.Channel, routingKey);
            }
            catch (Exception ex)
            {
                outbox.RetryCount++;
                outbox.Error = ex.Message;

                logger.LogWarning(ex,
                    "Failed to publish outbox message {Id}. Attempt {Retry}/{Max}.",
                    outbox.Id, outbox.RetryCount, MaxRetries);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string ToRoutingKey(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Email     => "notification.email",
        NotificationChannel.WhatsApp  => "notification.whatsapp",
        NotificationChannel.Push      => "notification.push",
        _                             => "notification.unknown"
    };

    // ── Cleanup ───────────────────────────────────────────────────────────

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel    is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
