using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using ACMESharp.Authorizations;
using ACMESharp.Protocol;

using DnsClient;

using DurableTask.TypedProxy;

using KeyVault.Acmebot.Contracts;
using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Models;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Management.Dns;
using Microsoft.Azure.Management.Dns.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Options;

namespace KeyVault.Acmebot
{
    public class SharedFunctions : ISharedFunctions
    {
        public SharedFunctions(IHttpClientFactory httpClientFactory, LookupClient lookupClient,
                               IAcmeProtocolClientFactory acmeProtocolClientFactory, IOptions<LetsEncryptOptions> options,
                               KeyVaultClient keyVaultClient, DnsManagementClient dnsManagementClient)
        {
            _httpClientFactory = httpClientFactory;
            _lookupClient = lookupClient;
            _acmeProtocolClientFactory = acmeProtocolClientFactory;
            _options = options.Value;
            _keyVaultClient = keyVaultClient;
            _dnsManagementClient = dnsManagementClient;
        }

        private const string InstanceIdKey = "InstanceId";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly LookupClient _lookupClient;
        private readonly IAcmeProtocolClientFactory _acmeProtocolClientFactory;
        private readonly LetsEncryptOptions _options;
        private readonly KeyVaultClient _keyVaultClient;
        private readonly DnsManagementClient _dnsManagementClient;

        [FunctionName(nameof(IssueCertificate))]
        public async Task IssueCertificate([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var dnsNames = context.GetInput<string[]>();

            var activity = context.CreateActivityProxy<ISharedFunctions>();

            // 前提条件をチェック
            await activity.Dns01Precondition(dnsNames);

            // 新しく ACME Order を作成する
            var orderDetails = await activity.Order(dnsNames);

            // 複数の Authorizations を処理する
            var challenges = new List<AcmeChallengeResult>();

            foreach (var authorization in orderDetails.Payload.Authorizations)
            {
                // ACME Challenge を実行
                var result = await activity.Dns01Authorization((authorization, context.ParentInstanceId ?? context.InstanceId));

                // Azure DNS で正しくレコードが引けるか確認
                await activity.CheckDnsChallenge(result);

                challenges.Add(result);
            }

            // ACME Answer を実行
            await activity.AnswerChallenges(challenges);

            // Order のステータスが ready になるまで 60 秒待機
            await activity.CheckIsReady(orderDetails);

            await activity.FinalizeOrder((dnsNames, orderDetails));
        }

        [FunctionName(nameof(GetCertificates))]
        public async Task<IList<CertificateBundle>> GetCertificates([ActivityTrigger] DateTime currentDateTime)
        {
            var certificates = await _keyVaultClient.GetAllCertificatesAsync(_options.VaultBaseUrl);

            var list = certificates.Where(x => x.Tags != null && x.Tags.TryGetValue("Issuer", out var issuer) && issuer == "letsencrypt.org")
                                   .Where(x => (x.Attributes.Expires.Value - currentDateTime).TotalDays < 30)
                                   .ToArray();

            var bundles = new List<CertificateBundle>();

            foreach (var item in list)
            {
                bundles.Add(await _keyVaultClient.GetCertificateAsync(item.Id));
            }

            return bundles;
        }

        [FunctionName(nameof(GetZones))]
        public Task<IList<Zone>> GetZones([ActivityTrigger] object input = null)
        {
            return _dnsManagementClient.Zones.ListAllAsync();
        }

        [FunctionName(nameof(Order))]
        public async Task<OrderDetails> Order([ActivityTrigger] string[] hostNames)
        {
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            return await acmeProtocolClient.CreateOrderAsync(hostNames);
        }

        [FunctionName(nameof(Dns01Precondition))]
        public async Task Dns01Precondition([ActivityTrigger] string[] hostNames)
        {
            // Azure DNS が存在するか確認
            var zones = await _dnsManagementClient.Zones.ListAllAsync();

            foreach (var hostName in hostNames)
            {
                if (!zones.Any(x => string.Equals(hostName, x.Name, StringComparison.OrdinalIgnoreCase) || hostName.EndsWith($".{x.Name}", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"Azure DNS zone \"{hostName}\" is not found");
                }
            }
        }

        [FunctionName(nameof(Dns01Authorization))]
        public async Task<AcmeChallengeResult> Dns01Authorization([ActivityTrigger] (string, string) input)
        {
            var (authzUrl, instanceId) = input;

            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            var authz = await acmeProtocolClient.GetAuthorizationDetailsAsync(authzUrl);

            // DNS-01 Challenge の情報を拾う
            var challenge = authz.Challenges.First(x => x.Type == "dns-01");

            var challengeValidationDetails = AuthorizationDecoder.ResolveChallengeForDns01(authz, challenge, acmeProtocolClient.Signer);

            // Azure DNS の TXT レコードを書き換え
            var zones = await _dnsManagementClient.Zones.ListAllAsync();

            var zone = zones.Where(x => challengeValidationDetails.DnsRecordName.EndsWith($".{x.Name}", StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(x => x.Name.Length)
                            .First();

            var resourceGroup = ExtractResourceGroup(zone.Id);

            // Challenge の詳細から Azure DNS 向けにレコード名を作成
            var acmeDnsRecordName = challengeValidationDetails.DnsRecordName.Replace($".{zone.Name}", "", StringComparison.OrdinalIgnoreCase);

            RecordSet recordSet;

            try
            {
                recordSet = await _dnsManagementClient.RecordSets.GetAsync(resourceGroup, zone.Name, acmeDnsRecordName, RecordType.TXT);
            }
            catch
            {
                recordSet = null;
            }

            if (recordSet != null)
            {
                if (recordSet.Metadata == null || !recordSet.Metadata.TryGetValue(InstanceIdKey, out var dnsInstanceId) || dnsInstanceId != instanceId)
                {
                    recordSet.Metadata = new Dictionary<string, string>
                    {
                        { InstanceIdKey, instanceId }
                    };

                    recordSet.TxtRecords.Clear();
                }

                recordSet.TTL = 60;

                // 既存の TXT レコードに値を追加する
                recordSet.TxtRecords.Add(new TxtRecord(new[] { challengeValidationDetails.DnsRecordValue }));
            }
            else
            {
                // 新しく TXT レコードを作成する
                recordSet = new RecordSet
                {
                    TTL = 60,
                    Metadata = new Dictionary<string, string>
                    {
                        { InstanceIdKey, instanceId }
                    },
                    TxtRecords = new[]
                    {
                        new TxtRecord(new[] { challengeValidationDetails.DnsRecordValue })
                    }
                };
            }

            await _dnsManagementClient.RecordSets.CreateOrUpdateAsync(resourceGroup, zone.Name, acmeDnsRecordName, RecordType.TXT, recordSet);

            return new AcmeChallengeResult
            {
                Url = challenge.Url,
                DnsRecordName = challengeValidationDetails.DnsRecordName,
                DnsRecordValue = challengeValidationDetails.DnsRecordValue
            };
        }

        [FunctionName(nameof(CheckDnsChallenge))]
        public async Task CheckDnsChallenge([ActivityTrigger] AcmeChallengeResult challenge)
        {
            // 実際に ACME の TXT レコードを引いて確認する
            var queryResult = await _lookupClient.QueryAsync(challenge.DnsRecordName, QueryType.TXT);

            var txtRecords = queryResult.Answers
                                        .OfType<DnsClient.Protocol.TxtRecord>()
                                        .ToArray();

            // レコードが存在しなかった場合はエラー
            if (txtRecords.Length == 0)
            {
                throw new RetriableActivityException($"{challenge.DnsRecordName} did not resolve.");
            }

            // レコードに今回のチャレンジが含まれていない場合もエラー
            if (!txtRecords.Any(x => x.Text.Contains(challenge.DnsRecordValue)))
            {
                throw new RetriableActivityException($"{challenge.DnsRecordName} value is not correct.");
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
        public async Task AnswerChallenges([ActivityTrigger] IList<AcmeChallengeResult> challenges)
        {
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            // Answer の準備が出来たことを通知
            foreach (var challenge in challenges)
            {
                await acmeProtocolClient.AnswerChallengeAsync(challenge.Url);
            }
        }

        [FunctionName(nameof(FinalizeOrder))]
        public async Task FinalizeOrder([ActivityTrigger] (string[], OrderDetails) input)
        {
            var (hostNames, orderDetails) = input;

            var certificateName = hostNames[0].Replace("*", "wildcard").Replace(".", "-");

            byte[] csr;

            try
            {
                // Key Vault を使って CSR を作成
                var request = await _keyVaultClient.CreateCertificateAsync(_options.VaultBaseUrl, certificateName, new CertificatePolicy
                {
                    X509CertificateProperties = new X509CertificateProperties
                    {
                        SubjectAlternativeNames = new SubjectAlternativeNames(dnsNames: hostNames)
                    }
                }, tags: new Dictionary<string, string>
                {
                    { "Issuer", "letsencrypt.org" }
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

            var httpClient = _httpClientFactory.CreateClient();

            var certificateData = await httpClient.GetByteArrayAsync(finalize.Payload.Certificate);

            // X509Certificate2Collection を作成
            var x509Certificates = new X509Certificate2Collection();

            x509Certificates.ImportFromPem(certificateData);

            await _keyVaultClient.MergeCertificateAsync(_options.VaultBaseUrl, certificateName, x509Certificates);
        }

        private static string ExtractResourceGroup(string resourceId)
        {
            var values = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);

            return values[3];
        }
    }
}