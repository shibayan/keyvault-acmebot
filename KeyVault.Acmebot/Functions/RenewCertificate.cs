// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using System.Threading.Tasks;

using DurableTask.TypedProxy;

using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

namespace KeyVault.Acmebot.Functions
{
    public class RenewCertificate
    {
        [FunctionName(nameof(RenewCertificate) + "_" + nameof(Orchestrator))]
        public async Task Orchestrator([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var activity = context.CreateActivityProxy<ISharedActivity>();

            var certificateName = context.GetInput<string>();
            var certificate = await activity.GetExpiringCertificate(certificateName);

            if (certificate == null)
            {
                log.LogInformation("Certificate was not found");

                return;
            }

            var dnsNames = certificate.DnsNames;

            log.LogInformation($"{certificate.Id} - {certificate.ExpiresOn}");

            try
            {
                // 証明書の更新処理を開始
                await context.CallSubOrchestratorAsync(nameof(SharedOrchestrator.IssueCertificate), dnsNames);
            }
            catch (Exception ex)
            {
                // 失敗した場合はログに詳細を書き出して続きを実行する
                log.LogError($"Failed sub orchestration with DNS names = {string.Join(",", dnsNames)}");
                log.LogError(ex.Message);
            }
        }

        [FunctionName(nameof(RenewCertificate) + "_" + nameof(EventGrid))]
        public async Task EventGrid([EventGridTrigger] EventGridEvent eventGridEvent, [DurableClient] IDurableClient starter, ILogger log)
        {
            if (eventGridEvent.EventType.Equals("Microsoft.KeyVault.CertificateNearExpiry", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var data = (JObject)eventGridEvent.Data;
                    var certificateName = data.Value<string>("objectName");

                    var instanceId = await starter.StartNewAsync<string>(nameof(RenewCertificate) + "_" + nameof(Orchestrator), certificateName);

                    log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
                }
                catch (Exception ex)
                {
                    log.LogError($"Unable to start orchestration due to possible malformed event. Event Id: {eventGridEvent.Id}");
                    throw;
                }
            }
        }
    }
}
