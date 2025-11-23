using KeyVault.Acmebot.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KeyVault.Acmebot.Internal;

public class WebhookInvoker
{
    public WebhookInvoker(IWebhookPayloadBuilder webhookPayloadBuilder, IHttpClientFactory httpClientFactory, IOptions<AcmebotOptions> options, ILogger<WebhookInvoker> logger)
    {
        _webhookPayloadBuilder = webhookPayloadBuilder;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    private readonly IWebhookPayloadBuilder _webhookPayloadBuilder;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AcmebotOptions _options;
    private readonly ILogger<WebhookInvoker> _logger;

    public Task SendCompletedEventAsync(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint)
    {
        if (_options.Webhook is null)
        {
            return Task.CompletedTask;
        }

        var payload = _webhookPayloadBuilder.BuildCompleted(certificateName, expirationDate, dnsNames, acmeEndpoint);

        return SendEventAsync(payload);
    }

    public Task SendFailedEventAsync(string certificateName, IEnumerable<string> dnsNames)
    {
        if (_options.Webhook is null)
        {
            return Task.CompletedTask;
        }

        var payload = _webhookPayloadBuilder.BuildFailed(certificateName, dnsNames);

        return SendEventAsync(payload);
    }

    private async Task SendEventAsync(object payload)
    {
        var httpClient = _httpClientFactory.CreateClient();

        var response = await httpClient.PostAsync(_options.Webhook, payload);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed invoke webhook. Status Code = {ResponseStatusCode}, Reason = {ReadAsStringAsync}", response.StatusCode, await response.Content.ReadAsStringAsync());
        }
    }
}
