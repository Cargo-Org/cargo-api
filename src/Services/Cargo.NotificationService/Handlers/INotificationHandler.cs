namespace Cargo.NotificationService.Handlers;

/// <summary>
/// Implemented by each notification channel handler.
/// The consumer dispatches to the correct handler based on the message's Channel.
/// </summary>
public interface INotificationHandler
{
    Task HandleAsync(
        Cargo.BuildingBlocks.Messaging.NotificationMessage message,
        CancellationToken ct);
}
