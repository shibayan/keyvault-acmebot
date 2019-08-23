﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using ACMESharp.Authorizations;
using ACMESharp.Protocol;

using AzureKeyVault.LetsEncrypt.Internal;

using DnsClient;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Management.Dns;
using Microsoft.Azure.Management.Dns.Models;
using Microsoft.Azure.WebJobs;

namespace AzureKeyVault.LetsEncrypt
{
    public class SharedFunctions : ISharedFunctions
    {
        public SharedFunctions(HttpClient httpClient, LookupClient lookupClient, AcmeProtocolClient acmeProtocolClient,
                               KeyVaultClient keyVaultClient, DnsManagementClient dnsManagementClient)
        {
            _httpClient = httpClient;
            _lookupClient = lookupClient;
            _acmeProtocolClient = acmeProtocolClient;
            _keyVaultClient = keyVaultClient;
            _dnsManagementClient = dnsManagementClient;
        }

        private const string InstanceIdKey = "InstanceId";

        private readonly HttpClient _httpClient;
        private readonly LookupClient _lookupClient;
        private readonly AcmeProtocolClient _acmeProtocolClient;
        private readonly KeyVaultClient _keyVaultClient;
        private readonly DnsManagementClient _dnsManagementClient;

        [FunctionName(nameof(IssueCertificate))]
        public async Task IssueCertificate([OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var dnsNames = context.GetInput<string[]>();

            var proxy = context.CreateActivityProxy<ISharedFunctions>();

            // 前提条件をチェック
            await proxy.Dns01Precondition(dnsNames);

            // 新しく ACME Order を作成する
            var orderDetails = await proxy.Order(dnsNames);

            // 複数の Authorizations を処理する
            var challenges = new List<ChallengeResult>();

            foreach (var authorization in orderDetails.Payload.Authorizations)
            {
                // ACME Challenge を実行
                var result = await proxy.Dns01Authorization((authorization, context.ParentInstanceId ?? context.InstanceId));

                // Azure DNS で正しくレコードが引けるか確認
                await proxy.CheckDnsChallenge(result);

                challenges.Add(result);
            }

            // ACME Answer を実行
            await proxy.AnswerChallenges(challenges);

            // Order のステータスが ready になるまで 60 秒待機
            await proxy.CheckIsReady(orderDetails);

            await proxy.FinalizeOrder((dnsNames, orderDetails));
        }

        [FunctionName(nameof(GetCertificates))]
        public async Task<IList<CertificateBundle>> GetCertificates([ActivityTrigger] DateTime currentDateTime)
        {
            var certificates = await _keyVaultClient.GetCertificatesAsync(Settings.Default.VaultBaseUrl);

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

        [FunctionName(nameof(Order))]
        public Task<OrderDetails> Order([ActivityTrigger] string[] hostNames)
        {
            return _acmeProtocolClient.CreateOrderAsync(hostNames);
        }

        [FunctionName(nameof(Dns01Precondition))]
        public async Task Dns01Precondition([ActivityTrigger] string[] hostNames)
        {
            // Azure DNS が存在するか確認
            var zones = await _dnsManagementClient.Zones.ListAsync();

            foreach (var hostName in hostNames)
            {
                if (!zones.Any(x => hostName.EndsWith(x.Name)))
                {
                    throw new InvalidOperationException($"Azure DNS zone \"{hostName}\" is not found");
                }
            }
        }

        [FunctionName(nameof(Dns01Authorization))]
        public async Task<ChallengeResult> Dns01Authorization([ActivityTrigger] (string, string) input)
        {
            var (authzUrl, instanceId) = input;

            var authz = await _acmeProtocolClient.GetAuthorizationDetailsAsync(authzUrl);

            // DNS-01 Challenge の情報を拾う
            var challenge = authz.Challenges.First(x => x.Type == "dns-01");

            var challengeValidationDetails = AuthorizationDecoder.ResolveChallengeForDns01(authz, challenge, _acmeProtocolClient.Signer);

            // Azure DNS の TXT レコードを書き換え
            var zone = (await _dnsManagementClient.Zones.ListAsync()).First(x => challengeValidationDetails.DnsRecordName.EndsWith(x.Name));

            var resourceId = ParseResourceId(zone.Id);

            // Challenge の詳細から Azure DNS 向けにレコード名を作成
            var acmeDnsRecordName = challengeValidationDetails.DnsRecordName.Replace("." + zone.Name, "");

            RecordSet recordSet;

            try
            {
                recordSet = await _dnsManagementClient.RecordSets.GetAsync(resourceId.resourceGroup, zone.Name, acmeDnsRecordName, RecordType.TXT);
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

            await _dnsManagementClient.RecordSets.CreateOrUpdateAsync(resourceId.resourceGroup, zone.Name, acmeDnsRecordName, RecordType.TXT, recordSet);

            return new ChallengeResult
            {
                Url = challenge.Url,
                DnsRecordName = challengeValidationDetails.DnsRecordName,
                DnsRecordValue = challengeValidationDetails.DnsRecordValue
            };
        }

        [FunctionName(nameof(CheckDnsChallenge))]
        public async Task CheckDnsChallenge([ActivityTrigger] ChallengeResult challenge)
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
            orderDetails = await _acmeProtocolClient.GetOrderDetailsAsync(orderDetails.OrderUrl, orderDetails);

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
        public async Task AnswerChallenges([ActivityTrigger] IList<ChallengeResult> challenges)
        {
            // Answer の準備が出来たことを通知
            foreach (var challenge in challenges)
            {
                await _acmeProtocolClient.AnswerChallengeAsync(challenge.Url);
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
                var request = await _keyVaultClient.CreateCertificateAsync(Settings.Default.VaultBaseUrl, certificateName, new CertificatePolicy
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
                var base64Csr = await _keyVaultClient.GetPendingCertificateSigningRequestAsync(Settings.Default.VaultBaseUrl, certificateName);

                csr = Convert.FromBase64String(base64Csr);
            }

            // Order の最終処理を実行し、証明書を作成
            var finalize = await _acmeProtocolClient.FinalizeOrderAsync(orderDetails.Payload.Finalize, csr);

            var certificateData = await _httpClient.GetByteArrayAsync(finalize.Payload.Certificate);

            // X509Certificate2Collection を作成
            var x509Certificates = new X509Certificate2Collection();

            x509Certificates.ImportFromPem(certificateData);

            await _keyVaultClient.MergeCertificateAsync(Settings.Default.VaultBaseUrl, certificateName, x509Certificates);
        }

        private static (string subscription, string resourceGroup, string provider) ParseResourceId(string resourceId)
        {
            var values = resourceId.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            return (values[1], values[3], values[5]);
        }
    }

    public class ChallengeResult
    {
        public string Url { get; set; }
        public string DnsRecordName { get; set; }
        public string DnsRecordValue { get; set; }
    }
}