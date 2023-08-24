using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

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

    public Task SendFailedEventAsync(string functionName, string reason)
    {
        if (_options.Webhook is null)
        {
            return Task.CompletedTask;
        }

        var payload = _webhookPayloadBuilder.BuildFailed(functionName, reason);

        return SendEventAsync(payload);
    }

    private async Task SendEventAsync(object payload)
    {
        var httpClient = _httpClientFactory.CreateClient();

        var response = await httpClient.PostAsync(_options.Webhook, payload);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning($"Failed invoke webhook. Status Code = {response.StatusCode}, Reason = {await response.Content.ReadAsStringAsync()}");
        }
    }
}
