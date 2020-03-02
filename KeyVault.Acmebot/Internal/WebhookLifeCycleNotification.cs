using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Options;

namespace KeyVault.Acmebot.Internal
{
    internal class WebhookLifeCycleNotification : ILifeCycleNotificationHelper
    {
        public WebhookLifeCycleNotification(IHttpClientFactory httpClientFactory, IOptions<LetsEncryptOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly LetsEncryptOptions _options;

        public Task OrchestratorStartingAsync(string hubName, string functionName, string instanceId, bool isReplay)
        {
            return Task.CompletedTask;
        }

        public Task OrchestratorCompletedAsync(string hubName, string functionName, string instanceId, bool continuedAsNew, bool isReplay)
        {
            return Task.CompletedTask;
        }

        public async Task OrchestratorFailedAsync(string hubName, string functionName, string instanceId, string reason, bool isReplay)
        {
            await PostEventAsync(functionName, instanceId, reason);
        }

        public Task OrchestratorTerminatedAsync(string hubName, string functionName, string instanceId, string reason)
        {
            return Task.CompletedTask;
        }

        private async Task PostEventAsync(string functionName, string instanceId, string reason)
        {
            if (string.IsNullOrEmpty(_options.Webhook))
            {
                return;
            }

            object model;

            if (_options.Webhook.Contains("hooks.slack.com"))
            {
                model = new
                {
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
                    instanceId,
                    reason
                };
            }

            var httpClient = _httpClientFactory.CreateClient();

            await httpClient.PostAsJsonAsync(_options.Webhook, model);
        }
    }
}
