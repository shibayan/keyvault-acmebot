using System.Threading;
using System.Threading.Tasks;

using DurableTask.TypedProxy;

using KeyVault.Acmebot.Models;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace KeyVault.Acmebot.Functions;

public class SharedOrchestrator
{
    [FunctionName(nameof(IssueCertificate))]
    public async Task IssueCertificate([OrchestrationTrigger] IDurableOrchestrationContext context)
    {
        var certificatePolicyItem = context.GetInput<CertificatePolicyItem>();

        var activity = context.CreateActivityProxy<ISharedActivity>();

        // 前提条件をチェック
        certificatePolicyItem.DnsProviderName = await activity.Dns01Precondition(certificatePolicyItem);

        // 新しく ACME Order を作成する
        var orderDetails = await activity.Order(certificatePolicyItem.DnsNames);

        // 既に確認済みの場合は Challenge をスキップする
        if (orderDetails.Payload.Status != "ready")
        {
            // ACME DNS-01 Challenge を実行
            var (challengeResults, propagationSeconds) = await activity.Dns01Authorization((certificatePolicyItem.DnsProviderName, certificatePolicyItem.DnsAlias, orderDetails.Payload.Authorizations));

            // DNS Provider が指定した分だけ後続の処理を遅延させる
            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(propagationSeconds), CancellationToken.None);

            // 正しく追加した DNS TXT レコードが引けるか確認
            await activity.CheckDnsChallenge(challengeResults);

            // ACME Answer Challenge を実行
            await activity.AnswerChallenges(challengeResults);

            // ACME Order のステータスが ready になるまで 60 秒待機
            await activity.CheckIsReady((orderDetails, challengeResults));

            // 作成した DNS レコードを削除
            await activity.CleanupDnsChallenge((certificatePolicyItem.DnsProviderName, challengeResults));
        }

        // Key Vault で CSR を作成し Finalize を実行
        orderDetails = await activity.FinalizeOrder((certificatePolicyItem, orderDetails));

        // Finalize の時点でステータスが valid の時点はスキップ
        if (orderDetails.Payload.Status != "valid")
        {
            // Finalize 後のステータスが valid になるまで 60 秒待機
            orderDetails = await activity.CheckIsValid(orderDetails);
        }

        // 証明書をダウンロードし Key Vault に保存された秘密鍵とマージ
        var certificate = await activity.MergeCertificate((certificatePolicyItem.CertificateName, orderDetails));

        // 証明書の更新が完了後に Webhook を送信する
        await activity.SendCompletedEvent((certificate.Name, certificate.ExpiresOn, certificatePolicyItem.DnsNames));
    }
}
