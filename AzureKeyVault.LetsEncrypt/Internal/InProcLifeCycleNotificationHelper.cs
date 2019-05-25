using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace AzureKeyVault.LetsEncrypt.Internal
{
    internal class InProcLifeCycleNotificationHelper : ILifeCycleNotificationHelper
    {
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

        private static readonly HttpClient _httpClient = new HttpClient();

        private static async Task PostEventAsync(string functionName, string instanceId, string reason)
        {
            if (string.IsNullOrEmpty(Settings.Default.Webhook))
            {
                return;
            }

            object model;

            if (Settings.Default.Webhook.Contains("hooks.slack.com"))
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
            else
            {
                model = new
                {
                    functionName,
                    instanceId,
                    reason
                };
            }

            await _httpClient.PostAsJsonAsync(Settings.Default.Webhook, model);
        }
    }
}
