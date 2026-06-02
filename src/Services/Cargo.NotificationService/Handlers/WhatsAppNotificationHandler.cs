using Cargo.BuildingBlocks.Messaging;
using Cargo.BuildingBlocks.Notifications.WhatsApp;

namespace Cargo.NotificationService.Handlers;

/// <summary>
/// Handles NotificationChannel.WhatsApp messages by forwarding to the Wireweb API.
/// </summary>
public sealed class WhatsAppNotificationHandler(
    IWhatsAppService whatsAppService,
    ILogger<WhatsAppNotificationHandler> logger) : INotificationHandler
{
    public async Task HandleAsync(NotificationMessage message, CancellationToken ct)
    {
        if (!message.Payload.TryGetValue("PhoneNumber", out var phoneNumber) ||
            !message.Payload.TryGetValue("Message",     out var text))
        {
            logger.LogError(
                "WhatsApp message {Id} is missing required payload fields (PhoneNumber, Message). Skipping.",
                message.Id);
            return;
        }

        var success = await whatsAppService.SendMessageAsync(phoneNumber, text, ct);

        if (success)
            logger.LogInformation(
                "WhatsApp message delivered to {PhoneNumber}.", phoneNumber);
        else
            logger.LogWarning(
                "WhatsApp delivery failed for message {Id} to {PhoneNumber}.",
                message.Id, phoneNumber);
    }
}
