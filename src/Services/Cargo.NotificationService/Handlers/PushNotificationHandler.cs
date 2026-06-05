using Cargo.BuildingBlocks.Messaging;

namespace Cargo.NotificationService.Handlers;

/// <summary>
/// Stub for future mobile push notifications (FCM / APNs).
/// Register mobile device tokens here once the mobile apps are built.
/// </summary>
public sealed class PushNotificationHandler(
    ILogger<PushNotificationHandler> logger) : INotificationHandler
{
    public Task HandleAsync(NotificationMessage message, CancellationToken ct)
    {
        logger.LogInformation(
            "Push notification {Id} received but mobile push is not yet implemented. " +
            "DeviceToken: {Token}, Title: {Title}",
            message.Id,
            message.Payload.GetValueOrDefault("DeviceToken", "N/A"),
            message.Payload.GetValueOrDefault("Title", "N/A"));

        return Task.CompletedTask;
    }
}
