using System;
using System.Threading;
using System.Threading.Tasks;

using DurableTask.TypedProxy;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Timer;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Functions;

public class RenewCertificates
{
    [Function($"{nameof(RenewCertificates)}_{nameof(Orchestrator)}")]
    public async Task Orchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var activity = context.CreateActivityProxy<ISharedActivity>();

        // 期限切れまで 30 日以内の証明書を取得する
        var certificates = await activity.GetExpiringCertificates(context.CurrentUtcDateTime);

        // 更新対象となる証明書がない場合は終わる
        if (certificates.Count == 0)
        {
            context.SetCustomStatus("Certificates are not found");
            return;
        }

        // スロットリング対策として 600 秒以内でジッターを追加する
        var jitter = (uint)context.NewGuid().GetHashCode() % 600;

        context.SetCustomStatus($"Adding random delay = {jitter}");

        await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(jitter), CancellationToken.None);

        // 証明書の更新を行う
        foreach (var certificate in certificates)
        {
            context.SetCustomStatus($"Certificate: {certificate.Id} - {certificate.ExpiresOn}");

            try
            {
                // 証明書の更新処理を開始
                var certificatePolicyItem = await activity.GetCertificatePolicy(certificate.Name);

                await context.CallSubOrchestratorWithRetryAsync(nameof(SharedOrchestrator.IssueCertificate), _retryOptions, certificatePolicyItem);
            }
            catch (Exception ex)
            {
                // 失敗した場合はログに詳細を書き出して続きを実行する
                context.SetCustomStatus($"Failed sub orchestration with DNS names = {string.Join(",", certificate.DnsNames)}: {ex.Message}");
            }
        }
    }

    [Function($"{nameof(RenewCertificates)}_{nameof(Timer)}")]
    public async Task Timer([TimerTrigger("0 0 0 * * *")] object timerInfo, [DurableClient] DurableTaskClient starter)
    {
        
        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync($"{nameof(RenewCertificates)}_{nameof(Orchestrator)}");

        _logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");
    }

    private readonly TaskOptions _retryOptions = new TaskOptions(TimeSpan.FromHours(3), 2)
    {
        Handle = ex => ex.InnerException?.InnerException is RetriableOrchestratorException
    };
}
