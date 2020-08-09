using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using ACMESharp.Authorizations;
using ACMESharp.Protocol;

using DnsClient;

using DurableTask.TypedProxy;

using KeyVault.Acmebot.Contracts;
using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Models;
using KeyVault.Acmebot.Options;
using KeyVault.Acmebot.Providers;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Options;

namespace KeyVault.Acmebot
{
    public class SharedFunctions : ISharedFunctions
    {
        public SharedFunctions(LookupClient lookupClient, IAcmeProtocolClientFactory acmeProtocolClientFactory,
                               IDnsProvider dnsProvider, KeyVaultClient keyVaultClient,
                               WebhookClient webhookClient, IOptions<AcmebotOptions> options)
        {
            _acmeProtocolClientFactory = acmeProtocolClientFactory;
            _dnsProvider = dnsProvider;
            _lookupClient = lookupClient;
            _keyVaultClient = keyVaultClient;
            _webhookClient = webhookClient;
            _options = options.Value;
        }

        private readonly LookupClient _lookupClient;
        private readonly IAcmeProtocolClientFactory _acmeProtocolClientFactory;
        private readonly IDnsProvider _dnsProvider;
        private readonly KeyVaultClient _keyVaultClient;
        private readonly WebhookClient _webhookClient;
        private readonly AcmebotOptions _options;

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
            await activity.CheckIsReady(orderDetails);

            var certificate = await activity.FinalizeOrder((dnsNames, orderDetails));

            // 証明書の更新が完了後に Webhook を送信する
            await activity.SendCompletedEvent((certificate.SecretIdentifier.Name, certificate.Attributes.Expires, dnsNames));
        }

        [FunctionName(nameof(GetExpiringCertificates))]
        public async Task<IList<CertificateBundle>> GetExpiringCertificates([ActivityTrigger] DateTime currentDateTime)
        {
            var certificates = await _keyVaultClient.GetAllCertificatesAsync(_options.VaultBaseUrl);

            var list = certificates.Where(x => x.TagsFilter(IssuerName, _options.Endpoint))
                                   .Where(x => (x.Attributes.Expires.Value - currentDateTime).TotalDays < 30)
                                   .ToArray();

            var bundles = new List<CertificateBundle>();

            foreach (var item in list)
            {
                bundles.Add(await _keyVaultClient.GetCertificateAsync(item.Id));
            }

            return bundles;
        }

        [FunctionName(nameof(GetAllCertificates))]
        public async Task<IList<CertificateBundle>> GetAllCertificates([ActivityTrigger] object input = null)
        {
            var certificates = await _keyVaultClient.GetAllCertificatesAsync(_options.VaultBaseUrl);

            var bundles = new List<CertificateBundle>();

            foreach (var item in certificates)
            {
                bundles.Add(await _keyVaultClient.GetCertificateAsync(item.Id));
            }

            return bundles;
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
                // 実際に ACME の TXT レコードを引いて確認する
                var queryResult = await _lookupClient.QueryAsync(challengeResult.DnsRecordName, QueryType.TXT);

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
                    throw new RetriableActivityException($"{challengeResult.DnsRecordName} value is not correct.");
                }
            }
        }

        [FunctionName(nameof(CheckIsReady))]
        public async Task CheckIsReady([ActivityTrigger] OrderDetails orderDetails)
        {
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            orderDetails = await acmeProtocolClient.GetOrderDetailsAsync(orderDetails.OrderUrl, orderDetails);

            if (orderDetails.Payload.Status == "pending")
            {
                // pending の場合はリトライする
                throw new RetriableActivityException("ACME domain validation is pending.");
            }

            if (orderDetails.Payload.Status == "invalid")
            {
                // invalid の場合は最初から実行が必要なので失敗させる
                throw new InvalidOperationException("Invalid order status. Required retry at first.");
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

        [FunctionName(nameof(FinalizeOrder))]
        public async Task<CertificateBundle> FinalizeOrder([ActivityTrigger] (string[], OrderDetails) input)
        {
            var (dnsNames, orderDetails) = input;

            var certificateName = dnsNames[0].Replace("*", "wildcard").Replace(".", "-");

            byte[] csr;

            try
            {
                // Key Vault を使って CSR を作成
                var request = await _keyVaultClient.CreateCertificateAsync(_options.VaultBaseUrl, certificateName, new CertificatePolicy
                {
                    X509CertificateProperties = new X509CertificateProperties
                    {
                        SubjectAlternativeNames = new SubjectAlternativeNames(dnsNames: dnsNames)
                    }
                }, tags: new Dictionary<string, string>
                {
                    { "Issuer", IssuerName },
                    { "Endpoint", _options.Endpoint }
                });

                csr = request.Csr;
            }
            catch (KeyVaultErrorException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
            {
                var base64Csr = await _keyVaultClient.GetPendingCertificateSigningRequestAsync(_options.VaultBaseUrl, certificateName);

                csr = Convert.FromBase64String(base64Csr);
            }

            // Order の最終処理を実行し、証明書を作成
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            var finalize = await acmeProtocolClient.FinalizeOrderAsync(orderDetails.Payload.Finalize, csr);

            // 証明書をバイト配列としてダウンロード
            var certificateData = await acmeProtocolClient.GetOrderCertificateAsync(finalize);

            // X509Certificate2Collection を作成
            var x509Certificates = new X509Certificate2Collection();

            x509Certificates.ImportFromPem(certificateData);

            return await _keyVaultClient.MergeCertificateAsync(_options.VaultBaseUrl, certificateName, x509Certificates);
        }

        [FunctionName(nameof(SendCompletedEvent))]
        public Task SendCompletedEvent([ActivityTrigger] (string, DateTime?, string[]) input)
        {
            var (certificateName, expirationDate, dnsNames) = input;

            return _webhookClient.SendCompletedEventAsync(certificateName, expirationDate, dnsNames);
        }
    }
}
