namespace Cargo.BuildingBlocks.Notifications.WhatsApp;

public interface IWhatsAppService
{
    Task<bool> SendMessageAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default);
}