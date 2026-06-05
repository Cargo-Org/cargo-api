namespace Cargo.BuildingBlocks.Notifications.WhatsApp;

public sealed class WhatsAppSettings
{
    public const string SectionName = "WhatsApp";

    public string ApiKey { get; init; } = string.Empty;

    public string SessionId { get; init; } = string.Empty;
}