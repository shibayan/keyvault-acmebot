using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Functions;

public class RenewCertificates(ILogger<RenewCertificates> logger)
{
    [Function($"{nameof(RenewCertificates)}_{nameof(Orchestrator)}")]
    public async Task Orchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // 期限切れまで 30 日以内の証明書を取得する
        var certificates = await context.CallGetExpiringCertificatesAsync(context.CurrentUtcDateTime);

        // 更新対象となる証明書がない場合は終わる
        if (certificates.Count == 0)
        {
            logger.LogInformation("Certificates are not found");

            return;
        }

        // スロットリング対策として 600 秒以内でジッターを追加する
        var jitter = (uint)context.NewGuid().GetHashCode() % 600;

        logger.LogInformation("Adding random delay = " + jitter);

        await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(jitter), CancellationToken.None);

        // 証明書の更新を行う
        foreach (var certificate in certificates)
        {
            logger.LogInformation($"{certificate.Id} - {certificate.ExpiresOn}");

            try
            {
                // 証明書の更新処理を開始
                var certificatePolicyItem = await context.CallGetCertificatePolicyAsync(certificate.Name);

                await context.CallSubOrchestratorAsync(nameof(SharedOrchestrator.IssueCertificate), certificatePolicyItem, TaskOptions.FromRetryPolicy(_retryOptions));
            }
            catch (Exception ex)
            {
                // 失敗した場合はログに詳細を書き出して続きを実行する
                logger.LogError($"Failed sub orchestration with DNS names = {string.Join(",", certificate.DnsNames)}");
                logger.LogError(ex.Message);
            }
        }
    }

    [Function($"{nameof(RenewCertificates)}_{nameof(Timer)}")]
    public async Task Timer([TimerTrigger("0 0 0 * * *")] TimerInfo timer, [DurableClient] DurableTaskClient starter)
    {
        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync($"{nameof(RenewCertificates)}_{nameof(Orchestrator)}");

        logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");
    }

    private readonly RetryPolicy _retryOptions = new(2, TimeSpan.FromHours(3))
    {
        HandleFailure = taskFailureDetails => taskFailureDetails.IsCausedBy<RetriableOrchestratorException>()
    };
}
