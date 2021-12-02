using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using ACMESharp.Authorizations;
using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;

using Azure.Security.KeyVault.Certificates;

using DnsClient;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Models;
using KeyVault.Acmebot.Options;
using KeyVault.Acmebot.Providers;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Functions
{
    public class SharedActivity : ISharedActivity
    {
        public SharedActivity(LookupClient lookupClient, AcmeProtocolClientFactory acmeProtocolClientFactory,
                              IDnsProvider dnsProvider, CertificateClient certificateClient,
                              WebhookInvoker webhookInvoker, IOptions<AcmebotOptions> options, ILogger<SharedActivity> logger)
        {
            _acmeProtocolClientFactory = acmeProtocolClientFactory;
            _dnsProvider = dnsProvider;
            _lookupClient = lookupClient;
            _certificateClient = certificateClient;
            _webhookInvoker = webhookInvoker;
            _options = options.Value;
            _logger = logger;
        }

        private readonly LookupClient _lookupClient;
        private readonly AcmeProtocolClientFactory _acmeProtocolClientFactory;
        private readonly IDnsProvider _dnsProvider;
        private readonly CertificateClient _certificateClient;
        private readonly WebhookInvoker _webhookInvoker;
        private readonly AcmebotOptions _options;
        private readonly ILogger<SharedActivity> _logger;

        private const string IssuerName = "Acmebot";

        [FunctionName(nameof(GetExpiringCertificates))]
        public async Task<IReadOnlyList<CertificateItem>> GetExpiringCertificates([ActivityTrigger] DateTime currentDateTime)
        {
            var certificates = _certificateClient.GetPropertiesOfCertificatesAsync();

            var result = new List<CertificateItem>();

            await foreach (var certificate in certificates)
            {
                if (!certificate.IsAcmebotManaged(IssuerName, _options.Endpoint))
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
        public async Task<IReadOnlyList<CertificateItem>> GetAllCertificates([ActivityTrigger] object input = null)
        {
            var certificates = _certificateClient.GetPropertiesOfCertificatesAsync();

            var result = new List<CertificateItem>();

            await foreach (var certificate in certificates)
            {
                var certificateItem = (await _certificateClient.GetCertificateAsync(certificate.Name)).Value.ToCertificateItem();

                certificateItem.IsManaged = certificate.IsAcmebotManaged(IssuerName, _options.Endpoint);

                result.Add(certificateItem);
            }

            return result;
        }

        [FunctionName(nameof(GetZones))]
        public async Task<IReadOnlyList<string>> GetZones([ActivityTrigger] object input = null)
        {
            try
            {
                var zones = await _dnsProvider.ListZonesAsync();

                return zones.Select(x => x.Name).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        [FunctionName(nameof(GetCertificatePolicy))]
        public async Task<CertificatePolicyItem> GetCertificatePolicy([ActivityTrigger] string certificateName)
        {
            CertificatePolicy certificatePolicy = await _certificateClient.GetCertificatePolicyAsync(certificateName);

            var dnsNames = certificatePolicy.SubjectAlternativeNames.DnsNames.ToArray();

            return new CertificatePolicyItem
            {
                CertificateName = certificateName,
                DnsNames = dnsNames.Length > 0 ? dnsNames : new[] { certificatePolicy.Subject[3..] },
                KeyType = certificatePolicy.KeyType?.ToString(),
                KeySize = certificatePolicy.KeySize,
                KeyCurveName = certificatePolicy.KeyCurveName?.ToString(),
                ReuseKey = certificatePolicy.ReuseKey
            };
        }

        [FunctionName(nameof(RevokeCertificate))]
        public async Task RevokeCertificate([ActivityTrigger] string certificateName)
        {
            var response = await _certificateClient.GetCertificateAsync(certificateName);

            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            await acmeProtocolClient.RevokeCertificateAsync(response.Value.Cer);
        }

        [FunctionName(nameof(Order))]
        public async Task<OrderDetails> Order([ActivityTrigger] IReadOnlyList<string> dnsNames)
        {
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            return await acmeProtocolClient.CreateOrderAsync(dnsNames);
        }

        [FunctionName(nameof(Dns01Precondition))]
        public async Task Dns01Precondition([ActivityTrigger] IReadOnlyList<string> dnsNames)
        {
            // DNS zone が存在するか確認
            var zones = await _dnsProvider.ListZonesAsync();

            var foundZones = new HashSet<DnsZone>();
            var zoneNotFoundDnsNames = new List<string>();

            foreach (var dnsName in dnsNames)
            {
                var zone = zones.Where(x => string.Equals(dnsName, x.Name, StringComparison.OrdinalIgnoreCase) || dnsName.EndsWith($".{x.Name}", StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(x => x.Name.Length)
                                .FirstOrDefault();

                // マッチする DNS zone が見つからない場合はエラー
                if (zone == null)
                {
                    zoneNotFoundDnsNames.Add(dnsName);
                    continue;
                }

                foundZones.Add(zone);
            }

            if (zoneNotFoundDnsNames.Count > 0)
            {
                throw new PreconditionException($"DNS zone(s) are not found. DnsNames = {string.Join(",", zoneNotFoundDnsNames)}");
            }

            // DNS zone に移譲されている Name servers が正しいか検証
            foreach (var zone in foundZones)
            {
                // DNS provider が Name servers を返していなければスキップ
                if (zone.NameServers == null || zone.NameServers.Count == 0)
                {
                    continue;
                }

                // DNS provider が Name servers を返している場合は NS レコードを確認
                var queryResult = await _lookupClient.QueryAsync(zone.Name, QueryType.NS);

                // 最後の . が付いている場合があるので削除して統一
                var expectedNameServers = zone.NameServers
                                              .Select(x => x.TrimEnd('.'))
                                              .ToArray();

                var actualNameServers = queryResult.Answers
                                                   .OfType<DnsClient.Protocol.NsRecord>()
                                                   .Select(x => x.NSDName.Value.TrimEnd('.'))
                                                   .ToArray();

                // 処理対象の DNS zone から取得した NS と実際に引いた NS の値が一つも一致しない場合はエラー
                if (!actualNameServers.Intersect(expectedNameServers, StringComparer.OrdinalIgnoreCase).Any())
                {
                    throw new PreconditionException($"The delegated name server is not correct. DNS zone = {zone.Name}, Expected = {string.Join(",", expectedNameServers)}, Actual = {string.Join(",", actualNameServers)}");
                }
            }
        }

        [FunctionName(nameof(Dns01Authorization))]
        public async Task<(IReadOnlyList<AcmeChallengeResult>, int)> Dns01Authorization([ActivityTrigger] IReadOnlyList<string> authorizationUrls)
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

                await _dnsProvider.DeleteTxtRecordAsync(zone, acmeDnsRecordName);
                await _dnsProvider.CreateTxtRecordAsync(zone, acmeDnsRecordName, lookup.Select(x => x.DnsRecordValue));
            }

            return (challengeResults, _dnsProvider.PropagationSeconds);
        }

        [FunctionName(nameof(CheckDnsChallenge))]
        public async Task CheckDnsChallenge([ActivityTrigger] IReadOnlyList<AcmeChallengeResult> challengeResults)
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
        public async Task AnswerChallenges([ActivityTrigger] IReadOnlyList<AcmeChallengeResult> challengeResults)
        {
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            // Answer の準備が出来たことを通知
            foreach (var challengeResult in challengeResults)
            {
                await acmeProtocolClient.AnswerChallengeAsync(challengeResult.Url);
            }
        }

        [FunctionName(nameof(CheckIsReady))]
        public async Task CheckIsReady([ActivityTrigger] (OrderDetails, IReadOnlyList<AcmeChallengeResult>) input)
        {
            var (orderDetails, challengeResults) = input;

            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            orderDetails = await acmeProtocolClient.GetOrderDetailsAsync(orderDetails.OrderUrl, orderDetails);

            if (orderDetails.Payload.Status == "pending" || orderDetails.Payload.Status == "processing")
            {
                // pending か processing の場合はリトライする
                throw new RetriableActivityException($"ACME validation status is {orderDetails.Payload.Status}. It will retry automatically.");
            }

            if (orderDetails.Payload.Status == "invalid")
            {
                var problems = new List<Problem>();

                foreach (var challengeResult in challengeResults)
                {
                    var challenge = await acmeProtocolClient.GetChallengeDetailsAsync(challengeResult.Url);

                    if (challenge.Status != "invalid" || challenge.Error == null)
                    {
                        continue;
                    }

                    _logger.LogError($"ACME domain validation error: {JsonConvert.SerializeObject(challenge.Error)}");

                    problems.Add(challenge.Error);
                }

                // 全てのエラーが dns 関係の場合は Orchestrator からリトライさせる
                if (problems.All(x => x.Type == "urn:ietf:params:acme:error:dns"))
                {
                    throw new RetriableOrchestratorException("ACME validation status is invalid, but retriable error. It will retry automatically.");
                }

                // invalid の場合は最初から実行が必要なので失敗させる
                throw new InvalidOperationException($"ACME validation status is invalid. Required retry at first.\nLastError = {JsonConvert.SerializeObject(problems.Last())}");
            }
        }

        [FunctionName(nameof(FinalizeOrder))]
        public async Task<OrderDetails> FinalizeOrder([ActivityTrigger] (CertificatePolicyItem, OrderDetails) input)
        {
            var (certificatePolicyItem, orderDetails) = input;

            byte[] csr;

            try
            {
                var certificatePolicy = certificatePolicyItem.ToCertificatePolicy();

                var certificateOperation = await _certificateClient.StartCreateCertificateAsync(certificatePolicyItem.CertificateName, certificatePolicy, tags: new Dictionary<string, string>
                {
                    { "Issuer", IssuerName },
                    { "Endpoint", _options.Endpoint }
                });

                csr = certificateOperation.Properties.Csr;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                var certificateOperation = await _certificateClient.GetCertificateOperationAsync(certificatePolicyItem.CertificateName);

                csr = certificateOperation.Properties.Csr;
            }

            // Order の最終処理を実行する
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            return await acmeProtocolClient.FinalizeOrderAsync(orderDetails.Payload.Finalize, csr);
        }

        [FunctionName(nameof(CheckIsValid))]
        public async Task<OrderDetails> CheckIsValid([ActivityTrigger] OrderDetails orderDetails)
        {
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            orderDetails = await acmeProtocolClient.GetOrderDetailsAsync(orderDetails.OrderUrl, orderDetails);

            if (orderDetails.Payload.Status == "pending" || orderDetails.Payload.Status == "processing")
            {
                // pending か processing の場合はリトライする
                throw new RetriableActivityException($"Finalize request is {orderDetails.Payload.Status}. It will retry automatically.");
            }

            if (orderDetails.Payload.Status == "invalid")
            {
                // invalid の場合は最初から実行が必要なので失敗させる
                throw new InvalidOperationException("Finalize request is invalid. Required retry at first.");
            }

            return orderDetails;
        }

        [FunctionName(nameof(MergeCertificate))]
        public async Task<CertificateItem> MergeCertificate([ActivityTrigger] (string, OrderDetails) input)
        {
            var (certificateName, orderDetails) = input;

            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            // 証明書をダウンロードして Key Vault へ格納
            var x509Certificates = await acmeProtocolClient.GetOrderCertificateAsync(orderDetails, _options.PreferredChain);

            var exportedX509Certificates = x509Certificates.Cast<X509Certificate2>().Select(x => x.Export(X509ContentType.Pfx));

            var mergeCertificateOptions = new MergeCertificateOptions(
                certificateName,
                _options.MitigateChainOrder ? exportedX509Certificates.Reverse() : exportedX509Certificates
            );

            return (await _certificateClient.MergeCertificateAsync(mergeCertificateOptions)).Value.ToCertificateItem();
        }

        [FunctionName(nameof(CleanupDnsChallenge))]
        public async Task CleanupDnsChallenge([ActivityTrigger] IReadOnlyList<AcmeChallengeResult> challengeResults)
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
        public Task SendCompletedEvent([ActivityTrigger] (string, DateTimeOffset?, IReadOnlyList<string>) input)
        {
            var (certificateName, expirationDate, dnsNames) = input;

            return _webhookInvoker.SendCompletedEventAsync(certificateName, expirationDate, dnsNames);
        }
    }
}
