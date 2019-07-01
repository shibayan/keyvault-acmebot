using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AzureKeyVault.LetsEncrypt
{
    public static class RenewCertificates
    {
        [FunctionName("RenewCertificates")]
        public static async Task RunOrchestrator([OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        {
            var proxy = context.CreateActivityProxy<ISharedFunctions>();

            // 期限切れまで 30 日以内の証明書を取得する
            var certificates = await proxy.GetCertificates(context.CurrentUtcDateTime);

            // 更新対象となる証明書がない場合は終わる
            if (certificates.Count == 0)
            {
                log.LogInformation("Certificates are not found");

                return;
            }

            // 証明書の更新を行う
            foreach (var certificate in certificates)
            {
                log.LogInformation($"{certificate.Id} - {certificate.Attributes.Expires}");

                // 証明書の更新処理を開始
                await context.CallSubOrchestratorAsync(nameof(SharedFunctions.IssueCertificate), certificate.Policy.X509CertificateProperties.SubjectAlternativeNames.DnsNames);
            }
        }

        [FunctionName("RenewCertificates_Timer")]
        public static async Task TimerStart([TimerTrigger("0 0 0 * * *")] TimerInfo timer, [OrchestrationClient] DurableOrchestrationClient starter, ILogger log)
        {
            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync("RenewCertificates", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
        }
    }
}