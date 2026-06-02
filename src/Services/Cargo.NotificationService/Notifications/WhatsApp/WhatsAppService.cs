using System.Net.Http;
using System.Text;
using System.Text.Json;
using Cargo.BuildingBlocks.Notifications.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cargo.NotificationService.Notifications.WhatsApp;

public sealed class WhatsAppService : IWhatsAppService
{
    private const string ApiUrl =
        "https://app.wireweb.co.in/api/v1/messages";

    private readonly HttpClient _httpClient;
    private readonly WhatsAppSettings _settings;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(
        HttpClient httpClient,
        IOptions<WhatsAppSettings> settings,
        ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _settings   = settings.Value;
        _logger     = logger;
    }

    public async Task<bool> SendMessageAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            sessionId = _settings.SessionId,
            to        = phoneNumber,
            text      = message
        };

        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                _settings.ApiKey);

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await _httpClient.SendAsync(
                request,
                cancellationToken);

            var responseContent =
                await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
                return true;

            _logger.LogWarning(
                "WhatsApp API error ({StatusCode}): {Body}",
                response.StatusCode, responseContent);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp send failed for {PhoneNumber}.", phoneNumber);
            return false;
        }
    }
}
