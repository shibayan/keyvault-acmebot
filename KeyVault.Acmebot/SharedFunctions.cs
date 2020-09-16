using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using ACMESharp.Authorizations;
using ACMESharp.Protocol;

using Azure.Security.KeyVault.Certificates;

using DnsClient;

using DurableTask.TypedProxy;

using KeyVault.Acmebot.Contracts;
using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Models;
using KeyVault.Acmebot.Options;
using KeyVault.Acmebot.Providers;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KeyVault.Acmebot
{
    public class SharedFunctions : ISharedFunctions
    {
        public SharedFunctions(LookupClient lookupClient, IAcmeProtocolClientFactory acmeProtocolClientFactory,
                               IDnsProvider dnsProvider, CertificateClient certificateClient,
                               WebhookClient webhookClient, IOptions<AcmebotOptions> options, ILogger<SharedFunctions> logger)
        {
            _acmeProtocolClientFactory = acmeProtocolClientFactory;
            _dnsProvider = dnsProvider;
            _lookupClient = lookupClient;
            _certificateClient = certificateClient;
            _webhookClient = webhookClient;
            _options = options.Value;
            _logger = logger;
        }

        private readonly LookupClient _lookupClient;
        private readonly IAcmeProtocolClientFactory _acmeProtocolClientFactory;
        private readonly IDnsProvider _dnsProvider;
        private readonly CertificateClient _certificateClient;
        private readonly WebhookClient _webhookClient;
        private readonly AcmebotOptions _options;
        private readonly ILogger<SharedFunctions> _logger;

        private const string IssuerName = "Acmebot";

        [FunctionName(nameof(IssueCertificate))]
        public async Task IssueCertificate([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var dnsNames = context.GetInput<string[]>();

            var activity = context.CreateActivityProxy<ISharedFunctions>();

            // 前提条件をチェック
            await activity.Dns01Precondition(dnsNames);

            // 新しく ACME Order を作成する
            var orderDetails = await activity.Order(dnsNames);

            // ACME Challenge を実行
            var challengeResults = await activity.Dns01Authorization(orderDetails.Payload.Authorizations);

            // DNS で正しくレコードが引けるか確認
            await activity.CheckDnsChallenge(challengeResults);

            // ACME Answer を実行
            await activity.AnswerChallenges(challengeResults);

            // Order のステータスが ready になるまで 60 秒待機
            await activity.CheckIsReady((orderDetails, challengeResults));

            // 証明書を作成し Key Vault に保存
            var certificate = await activity.FinalizeOrder((dnsNames, orderDetails));

            // 作成した DNS レコードを削除
            await activity.CleanupDnsChallenge(challengeResults);

            // 証明書の更新が完了後に Webhook を送信する
            await activity.SendCompletedEvent((certificate.Name, certificate.ExpiresOn, dnsNames));
        }

        [FunctionName(nameof(GetExpiringCertificates))]
        public async Task<IList<CertificateItem>> GetExpiringCertificates([ActivityTrigger] DateTime currentDateTime)
        {
            var certificates = _certificateClient.GetPropertiesOfCertificatesAsync();

            var result = new List<CertificateItem>();

            await foreach (var certificate in certificates)
            {
                if (!certificate.TagsFilter(IssuerName, _options.Endpoint))
                {
                    continue;
                }

                if ((certificate.ExpiresOn.Value - currentDateTime).TotalDays > 30)
                {
                    continue;
                }

                result.Add((await _certificateClient.GetCertificateAsync(certificate.Name)).Value.ToCertificateItem());
            }

            return result;
        }

        [FunctionName(nameof(GetAllCertificates))]
        public async Task<IList<CertificateItem>> GetAllCertificates([ActivityTrigger] object input = null)
        {
            var certificates = _certificateClient.GetPropertiesOfCertificatesAsync();

            var result = new List<CertificateItem>();

            await foreach (var certificate in certificates)
            {
                result.Add((await _certificateClient.GetCertificateAsync(certificate.Name)).Value.ToCertificateItem());
            }

            return result;
        }

        [FunctionName(nameof(GetZones))]
        public async Task<IList<string>> GetZones([ActivityTrigger] object input = null)
        {
            var zones = await _dnsProvider.ListZonesAsync();

            return zones.Select(x => x.Name).ToArray();
        }

        [FunctionName(nameof(Order))]
        public async Task<OrderDetails> Order([ActivityTrigger] string[] dnsNames)
        {
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            return await acmeProtocolClient.CreateOrderAsync(dnsNames);
        }

        [FunctionName(nameof(Dns01Precondition))]
        public async Task Dns01Precondition([ActivityTrigger] string[] dnsNames)
        {
            // DNS zone が存在するか確認
            var zones = await _dnsProvider.ListZonesAsync();

            foreach (var dnsName in dnsNames)
            {
                if (!zones.Any(x => string.Equals(dnsName, x.Name, StringComparison.OrdinalIgnoreCase) || dnsName.EndsWith($".{x.Name}", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"DNS zone \"{dnsName}\" is not found");
                }
            }
        }

        [FunctionName(nameof(Dns01Authorization))]
        public async Task<IList<AcmeChallengeResult>> Dns01Authorization([ActivityTrigger] string[] authorizationUrls)
        {
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            var challengeResults = new List<AcmeChallengeResult>();

            foreach (var authorizationUrl in authorizationUrls)
            {
                // Authorization の詳細を取得
                var authorization = await acmeProtocolClient.GetAuthorizationDetailsAsync(authorizationUrl);

                // DNS-01 Challenge の情報を拾う
                var challenge = authorization.Challenges.First(x => x.Type == "dns-01");

                var challengeValidationDetails = AuthorizationDecoder.ResolveChallengeForDns01(authorization, challenge, acmeProtocolClient.Signer);

                // Challenge の情報を保存する
                challengeResults.Add(new AcmeChallengeResult
                {
                    Url = challenge.Url,
                    DnsRecordName = challengeValidationDetails.DnsRecordName,
                    DnsRecordValue = challengeValidationDetails.DnsRecordValue
                });
            }

            // DNS zone の一覧を取得する
            var zones = await _dnsProvider.ListZonesAsync();

            // DNS-01 の検証レコード名毎に DNS に TXT レコードを作成
            foreach (var lookup in challengeResults.ToLookup(x => x.DnsRecordName))
            {
                var dnsRecordName = lookup.Key;

                var zone = zones.Where(x => dnsRecordName.EndsWith($".{x.Name}", StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(x => x.Name.Length)
                                .First();

                // Challenge の詳細から DNS 向けにレコード名を作成
                var acmeDnsRecordName = dnsRecordName.Replace($".{zone.Name}", "", StringComparison.OrdinalIgnoreCase);

                await _dnsProvider.UpsertTxtRecordAsync(zone, acmeDnsRecordName, lookup.Select(x => x.DnsRecordValue));
            }

            return challengeResults;
        }

        [FunctionName(nameof(CheckDnsChallenge))]
        public async Task CheckDnsChallenge([ActivityTrigger] IList<AcmeChallengeResult> challengeResults)
        {
            foreach (var challengeResult in challengeResults)
            {
                IDnsQueryResponse queryResult;

                try
                {
                    // 実際に ACME の TXT レコードを引いて確認する
                    queryResult = await _lookupClient.QueryAsync(challengeResult.DnsRecordName, QueryType.TXT);
                }
                catch (DnsResponseException ex)
                {
                    // 一時的な DNS エラーの可能性があるためリトライ
                    throw new RetriableActivityException($"{challengeResult.DnsRecordName} bad response. Message: \"{ex.DnsError}\"", ex);
                }

                var txtRecords = queryResult.Answers
                                            .OfType<DnsClient.Protocol.TxtRecord>()
                                            .ToArray();

                // レコードが存在しなかった場合はエラー
                if (txtRecords.Length == 0)
                {
                    throw new RetriableActivityException($"{challengeResult.DnsRecordName} did not resolve.");
                }

                // レコードに今回のチャレンジが含まれていない場合もエラー
                if (!txtRecords.Any(x => x.Text.Contains(challengeResult.DnsRecordValue)))
                {
                    throw new RetriableActivityException($"{challengeResult.DnsRecordName} is not correct. Expected: \"{challengeResult.DnsRecordValue}\", Actual: \"{string.Join(",", txtRecords.SelectMany(x => x.Text))}\"");
                }
            }
        }

        [FunctionName(nameof(AnswerChallenges))]
        public async Task AnswerChallenges([ActivityTrigger] IList<AcmeChallengeResult> challengeResults)
        {
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            // Answer の準備が出来たことを通知
            foreach (var challengeResult in challengeResults)
            {
                await acmeProtocolClient.AnswerChallengeAsync(challengeResult.Url);
            }
        }

        [FunctionName(nameof(CheckIsReady))]
        public async Task CheckIsReady([ActivityTrigger] (OrderDetails, IList<AcmeChallengeResult>) input)
        {
            var (orderDetails, challengeResults) = input;

            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            orderDetails = await acmeProtocolClient.GetOrderDetailsAsync(orderDetails.OrderUrl, orderDetails);

            if (orderDetails.Payload.Status == "pending")
            {
                // pending の場合はリトライする
                throw new RetriableActivityException("ACME domain validation is pending.");
            }

            if (orderDetails.Payload.Status == "invalid")
            {
                object lastError = null;

                foreach (var challengeResult in challengeResults)
                {
                    var challenge = await acmeProtocolClient.GetChallengeDetailsAsync(challengeResult.Url);

                    if (challenge.Status != "invalid")
                    {
                        continue;
                    }

                    _logger.LogError($"ACME domain validation error: {challenge.Error}");

                    lastError = challenge.Error;
                }

                // invalid の場合は最初から実行が必要なので失敗させる
                throw new InvalidOperationException($"ACME domain validation is invalid. Required retry at first.\nLastError = {lastError}");
            }
        }

        [FunctionName(nameof(FinalizeOrder))]
        public async Task<CertificateItem> FinalizeOrder([ActivityTrigger] (string[], OrderDetails) input)
        {
            var (dnsNames, orderDetails) = input;

            var certificateName = dnsNames[0].Replace("*", "wildcard").Replace(".", "-");

            byte[] csr;

            try
            {
                // Key Vault を使って CSR を作成
                var subjectAlternativeNames = new SubjectAlternativeNames();

                foreach (var dnsName in dnsNames)
                {
                    subjectAlternativeNames.DnsNames.Add(dnsName);
                }

                var policy = new CertificatePolicy(WellKnownIssuerNames.Unknown, subjectAlternativeNames);

                var certificateOperation = await _certificateClient.StartCreateCertificateAsync(certificateName, policy, tags: new Dictionary<string, string>
                {
                    { "Issuer", IssuerName },
                    { "Endpoint", _options.Endpoint }
                });

                csr = certificateOperation.Properties.Csr;
            }
            catch
            {
                var certificateOperation = await _certificateClient.GetCertificateOperationAsync(certificateName);

                csr = certificateOperation.Properties.Csr;
            }

            // Order の最終処理を実行し、証明書を作成
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            var finalize = await acmeProtocolClient.FinalizeOrderAsync(orderDetails.Payload.Finalize, csr);

            // 証明書をバイト配列としてダウンロード
            var certificateData = await acmeProtocolClient.GetOrderCertificateAsync(finalize);

            // X509Certificate2Collection を作成
            var x509Certificates = new X509Certificate2Collection();

            x509Certificates.ImportFromPem(certificateData);

            var mergeCertificateOptions = new MergeCertificateOptions(
                certificateName,
                x509Certificates.OfType<X509Certificate2>().Select(x => x.Export(X509ContentType.Pfx))
            );

            return (await _certificateClient.MergeCertificateAsync(mergeCertificateOptions)).Value.ToCertificateItem();
        }

        [FunctionName(nameof(CleanupDnsChallenge))]
        public async Task CleanupDnsChallenge([ActivityTrigger] IList<AcmeChallengeResult> challengeResults)
        {
            // DNS zone の一覧を取得する
            var zones = await _dnsProvider.ListZonesAsync();

            // DNS-01 の検証レコード名毎に DNS から TXT レコードを削除
            foreach (var lookup in challengeResults.ToLookup(x => x.DnsRecordName))
            {
                var dnsRecordName = lookup.Key;

                var zone = zones.Where(x => dnsRecordName.EndsWith($".{x.Name}", StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(x => x.Name.Length)
                                .First();

                // Challenge の詳細から DNS 向けにレコード名を作成
                var acmeDnsRecordName = dnsRecordName.Replace($".{zone.Name}", "", StringComparison.OrdinalIgnoreCase);

                await _dnsProvider.DeleteTxtRecordAsync(zone, acmeDnsRecordName);
            }
        }

        [FunctionName(nameof(SendCompletedEvent))]
        public Task SendCompletedEvent([ActivityTrigger] (string, DateTimeOffset?, string[]) input)
        {
            var (certificateName, expirationDate, dnsNames) = input;

            return _webhookClient.SendCompletedEventAsync(certificateName, expirationDate, dnsNames);
        }
    }
}
