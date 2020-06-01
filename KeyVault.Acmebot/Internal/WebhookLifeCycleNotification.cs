using System.Threading.Tasks;

using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace KeyVault.Acmebot.Internal
{
    internal class WebhookLifeCycleNotification : ILifeCycleNotificationHelper
    {
        public WebhookLifeCycleNotification(WebhookClient webhookClient)
        {
            _webhookClient = webhookClient;
        }

        private readonly WebhookClient _webhookClient;

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
            await _webhookClient.SendFailedEventAsync(functionName, reason);
        }

        public Task OrchestratorTerminatedAsync(string hubName, string functionName, string instanceId, string reason)
        {
            return Task.CompletedTask;
        }
    }
}
