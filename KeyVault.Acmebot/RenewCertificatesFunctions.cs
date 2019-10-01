using System.Threading.Tasks;

using KeyVault.Acmebot.Contracts;

using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot
{
    public class RenewCertificatesFunctions
    {
        [FunctionName(nameof(RenewCertificates))]
        public async Task RenewCertificates([OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        {
            var activity = context.CreateActivityProxy<ISharedFunctions>();

            // 期限切れまで 30 日以内の証明書を取得する
            var certificates = await activity.GetCertificates(context.CurrentUtcDateTime);

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

        [FunctionName(nameof(RenewCertificates_Timer))]
        public static async Task RenewCertificates_Timer([TimerTrigger("0 0 0 * * 1,3,5")] TimerInfo timer, [OrchestrationClient] DurableOrchestrationClient starter, ILogger log)
        {
            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync(nameof(RenewCertificates), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
        }
    }
}