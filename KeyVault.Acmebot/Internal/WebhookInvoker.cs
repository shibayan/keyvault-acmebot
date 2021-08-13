using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using KeyVault.Acmebot.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Internal
{
    public class WebhookInvoker
    {
        public WebhookInvoker(IHttpClientFactory httpClientFactory, IOptions<AcmebotOptions> options, ILogger<WebhookInvoker> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AcmebotOptions _options;
        private readonly ILogger<WebhookInvoker> _logger;

        public Task SendCompletedEventAsync(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames)
        {
            if (string.IsNullOrEmpty(_options.Webhook))
            {
                return Task.CompletedTask;
            }

            object model;

            if (_options.Webhook.Contains("hooks.slack.com"))
            {
                model = new
                {
                    username = "Acmebot",
                    attachments = new[]
                    {
                        new
                        {
                            text = "A new certificate has been issued.",
                            color = "good",
                            fields = new object[]
                            {
                                new
                                {
                                    title = "Certificate Name",
                                    value= certificateName,
                                    @short = true
                                },
                                new
                                {
                                    title = "Expiration Date",
                                    value = expirationDate,
                                    @short = true
                                },
                                new
                                {
                                    title = "DNS Names",
                                    value = string.Join("\n", dnsNames)
                                }
                            }
                        }
                    }
                };
            }
            else if (_options.Webhook.Contains(".office.com"))
            {
                model = new
                {
                    title = "Acmebot",
                    text = $"A new certificate has been issued.\n\n**Certificate Name**: {certificateName}\n\n**Expiration Date**: {expirationDate}\n\n**DNS Names**: {string.Join(", ", dnsNames)}",
                    themeColor = "2EB886"
                };
            }
            else
            {
                model = new
                {
                    certificateName,
                    dnsNames
                };
            }

            return SendEventAsync(model);
        }

        public Task SendFailedEventAsync(string functionName, string reason)
        {
            if (string.IsNullOrEmpty(_options.Webhook))
            {
                return Task.CompletedTask;
            }

            object model;

            if (_options.Webhook.Contains("hooks.slack.com"))
            {
                model = new
                {
                    username = "Acmebot",
                    attachments = new[]
                    {
                        new
                        {
                            title = functionName,
                            text = reason,
                            color = "danger"
                        }
                    }
                };
            }
            else if (_options.Webhook.Contains(".office.com"))
            {
                model = new
                {
                    title = "Acmebot",
                    text = $"**{functionName}**\n\n**Reason**\n\n{reason}",
                    themeColor = "A30200"
                };
            }
            else
            {
                model = new
                {
                    functionName,
                    reason
                };
            }

            return SendEventAsync(model);
        }

        private async Task SendEventAsync(object model)
        {
            var httpClient = _httpClientFactory.CreateClient();

            var content = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(_options.Webhook, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed invoke webhook. Status Code = {response.StatusCode}, Reason = {await response.Content.ReadAsStringAsync()}");
            }
        }
    }
}
