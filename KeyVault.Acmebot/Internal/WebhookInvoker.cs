using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using KeyVault.Acmebot.Options;

using Microsoft.Extensions.Options;

namespace KeyVault.Acmebot.Internal
{
    public class WebhookInvoker
    {
        public WebhookInvoker(IHttpClientFactory httpClientFactory, IOptions<AcmebotOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AcmebotOptions _options;

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
            else if (_options.Webhook.Contains("outlook.office.com"))
            {
                model = new
                {
                    title = certificateName,
                    text = string.Join("\n", dnsNames),
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
            else if (_options.Webhook.Contains("outlook.office.com"))
            {
                model = new
                {
                    title = functionName,
                    text = reason,
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

            await httpClient.PostAsJsonAsync(_options.Webhook, model);
        }
    }
}
