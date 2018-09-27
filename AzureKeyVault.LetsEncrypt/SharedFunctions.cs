using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using ACMESharp.Authorizations;
using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Management.Dns;
using Microsoft.Azure.Management.Dns.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;

using Newtonsoft.Json;

namespace AzureKeyVault.LetsEncrypt
{
    public static class SharedFunctions
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly HttpClient _acmeHttpClient = new HttpClient { BaseAddress = new Uri("https://acme-v02.api.letsencrypt.org/") };

        [FunctionName(nameof(IssueCertificate))]
        public static async Task IssueCertificate([OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        {
            var dnsNames = context.GetInput<string[]>();

            // 前提条件をチェック
            await context.CallActivityAsync(nameof(Dns01Precondition), dnsNames);

            // 新しく ACME Order を作成する
            var orderDetails = await context.CallActivityAsync<OrderDetails>(nameof(Order), dnsNames);

            // 複数の Authorizations を処理する
            var challenges = new List<Challenge>();

            foreach (var authorization in orderDetails.Payload.Authorizations)
            {
                // ACME Challenge を実行
                challenges.Add(await context.CallActivityAsync<Challenge>(nameof(Dns01Authorization), authorization));
            }

            // Order status が ready になるまで待つ
            await context.CallActivityAsync(nameof(AnswerChallenges), (orderDetails, challenges));

            await context.CallActivityAsync(nameof(FinalizeOrder), (dnsNames, orderDetails));
        }

        [FunctionName(nameof(GetCertificates))]
        public static async Task<IList<CertificateBundle>> GetCertificates([ActivityTrigger] DurableActivityContext context, ILogger log)
        {
            var currentDateTime = context.GetInput<DateTime>();

            var keyVaultClient = CreateKeyVaultClient();

            var certificates = await keyVaultClient.GetCertificatesAsync(Settings.Default.VaultBaseUrl);

            var list = certificates.Where(x => x.Tags != null && x.Tags.TryGetValue("Issuer", out var issuer) && issuer == "letsencrypt.org")
                                   .Where(x => (x.Attributes.Expires.Value - currentDateTime).TotalDays < 30)
                                   .ToArray();

            var bundles = new List<CertificateBundle>();

            foreach (var item in list)
            {
                bundles.Add(await keyVaultClient.GetCertificateAsync(item.Id));
            }

            return bundles;
        }

        [FunctionName(nameof(Order))]
        public static async Task<OrderDetails> Order([ActivityTrigger] DurableActivityContext context, ILogger log)
        {
            var hostNames = context.GetInput<string[]>();

            var acme = await CreateAcmeClientAsync();

            return await acme.CreateOrderAsync(hostNames);
        }

        [FunctionName(nameof(Dns01Precondition))]
        public static async Task Dns01Precondition([ActivityTrigger] DurableActivityContext context, ILogger log)
        {
            var hostNames = context.GetInput<string[]>();

            var dnsClient = await CreateDnsManagementClientAsync();

            // Azure DNS が存在するか確認
            var zones = await dnsClient.Zones.ListAsync();

            foreach (var hostName in hostNames)
            {
                if (!zones.Any(x => hostName.EndsWith(x.Name)))
                {
                    log.LogError($"Azure DNS zone \"{hostNames}\" is not found");

                    throw new InvalidOperationException();
                }
            }
        }

        [FunctionName(nameof(Dns01Authorization))]
        public static async Task<Challenge> Dns01Authorization([ActivityTrigger] DurableActivityContext context, ILogger log)
        {
            var authzUrl = context.GetInput<string>();

            var acme = await CreateAcmeClientAsync();

            var authz = await acme.GetAuthorizationDetailsAsync(authzUrl);

            // DNS-01 Challenge の情報を拾う
            var challenge = authz.Challenges.First(x => x.Type == "dns-01");

            var challengeValidationDetails = AuthorizationDecoder.ResolveChallengeForDns01(authz, challenge, acme.Signer);

            // Azure DNS の TXT レコードを書き換え
            var dnsClient = await CreateDnsManagementClientAsync();

            var zone = (await dnsClient.Zones.ListAsync()).First(x => challengeValidationDetails.DnsRecordName.EndsWith(x.Name));

            var resourceId = ParseResourceId(zone.Id);

            // Challenge の詳細から Azure DNS 向けにレコード名を作成
            var acmeDnsRecordName = challengeValidationDetails.DnsRecordName.Replace("." + zone.Name, "");

            RecordSet recordSet;

            try
            {
                recordSet = await dnsClient.RecordSets.GetAsync(resourceId["resourceGroups"], zone.Name, acmeDnsRecordName, RecordType.TXT);
            }
            catch
            {
                recordSet = null;
            }

            if (recordSet != null)
            {
                if (recordSet.Metadata == null || !recordSet.Metadata.TryGetValue(nameof(context.InstanceId), out var instanceId) || instanceId != context.InstanceId)
                {
                    recordSet.Metadata = new Dictionary<string, string>
                    {
                        { nameof(context.InstanceId), context.InstanceId }
                    };

                    recordSet.TxtRecords.Clear();
                }

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
                        { nameof(context.InstanceId), context.InstanceId }
                    },
                    TxtRecords = new[]
                    {
                        new TxtRecord(new[] { challengeValidationDetails.DnsRecordValue })
                    }
                };
            }

            await dnsClient.RecordSets.CreateOrUpdateAsync(resourceId["resourceGroups"], zone.Name, acmeDnsRecordName, RecordType.TXT, recordSet);

            return challenge;
        }

        [FunctionName(nameof(AnswerChallenges))]
        public static async Task AnswerChallenges([ActivityTrigger] DurableActivityContext context, ILogger log)
        {
            var (orderDetails, challenges) = context.GetInput<(OrderDetails, IList<Challenge>)>();

            var acme = await CreateAcmeClientAsync();

            // Answer の準備が出来たことを通知
            foreach (var challenge in challenges)
            {
                await acme.AnswerChallengeAsync(challenge.Url);
            }

            // Order のステータスが ready になるまで 60 秒待機
            for (int i = 0; i < 12; i++)
            {
                orderDetails = await acme.GetOrderDetailsAsync(orderDetails.OrderUrl, orderDetails);

                if (orderDetails.Payload.Status == "ready")
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            log.LogError($"Timeout ACME challenge status : {orderDetails.Payload.Status}");

            if (orderDetails.Payload.Error != null)
            {
                log.LogError($"{orderDetails.Payload.Error.Type},{orderDetails.Payload.Error.Status},{orderDetails.Payload.Error.Detail}");
            }

            throw new InvalidOperationException();
        }

        [FunctionName(nameof(FinalizeOrder))]
        public static async Task FinalizeOrder([ActivityTrigger] DurableActivityContext context, ILogger log)
        {
            var (hostNames, orderDetails) = context.GetInput<(string[], OrderDetails)>();

            var certificateName = hostNames[0].Replace("*", "wildcard").Replace(".", "-");

            var keyVaultClient = CreateKeyVaultClient();

            // Key Vault を使って CSR を作成
            var request = await keyVaultClient.CreateCertificateAsync(Settings.Default.VaultBaseUrl, certificateName, new CertificatePolicy
            {
                X509CertificateProperties = new X509CertificateProperties
                {
                    SubjectAlternativeNames = new SubjectAlternativeNames(dnsNames: hostNames)
                }
            }, tags: new Dictionary<string, string>
            {
                { "Issuer", "letsencrypt.org" }
            });

            var acme = await CreateAcmeClientAsync();

            // Order の最終処理を実行し、証明書を作成
            var finalize = await acme.FinalizeOrderAsync(orderDetails.Payload.Finalize, request.Csr);

            var certificateData = await _httpClient.GetByteArrayAsync(finalize.Payload.Certificate);

            // X509Certificate2 を作成
            var certificate = new X509Certificate2(certificateData);

            await keyVaultClient.MergeCertificateAsync(Settings.Default.VaultBaseUrl, certificateName, new X509Certificate2Collection(certificate));
        }

        private static async Task<AcmeProtocolClient> CreateAcmeClientAsync()
        {
            var account = default(AccountDetails);
            var accountKey = default(AccountKey);
            var acmeDir = default(ServiceDirectory);

            LoadState(ref account, "account.json");
            LoadState(ref accountKey, "account_key.json");
            LoadState(ref acmeDir, "directory.json");

            var acme = new AcmeProtocolClient(_acmeHttpClient, acmeDir, account, accountKey?.GenerateSigner());

            if (acmeDir == null)
            {
                acmeDir = await acme.GetDirectoryAsync();

                SaveState(acmeDir, "directory.json");

                acme.Directory = acmeDir;
            }

            await acme.GetNonceAsync();

            if (account == null || accountKey == null)
            {
                account = await acme.CreateAccountAsync(new[] { "mailto:" + Settings.Default.Contacts }, true);

                accountKey = new AccountKey
                {
                    KeyType = acme.Signer.JwsAlg,
                    KeyExport = acme.Signer.Export()
                };

                SaveState(account, "account.json");
                SaveState(accountKey, "account_key.json");

                acme.Account = account;
            }

            return acme;
        }

        private static void LoadState<T>(ref T value, string path)
        {
            var fullPath = Environment.ExpandEnvironmentVariables(@"%HOME%\.acme\" + path);

            if (!File.Exists(fullPath))
            {
                return;
            }

            var json = File.ReadAllText(fullPath);

            value = JsonConvert.DeserializeObject<T>(json);
        }

        private static void SaveState<T>(T value, string path)
        {
            var fullPath = Environment.ExpandEnvironmentVariables(@"%HOME%\.acme\" + path);
            var directoryPath = Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var json = JsonConvert.SerializeObject(value, Formatting.Indented);

            File.WriteAllText(fullPath, json);
        }

        private static async Task<DnsManagementClient> CreateDnsManagementClientAsync()
        {
            var tokenProvider = new AzureServiceTokenProvider();

            var accessToken = await tokenProvider.GetAccessTokenAsync("https://management.azure.com/");

            var dnsClient = new DnsManagementClient(new TokenCredentials(accessToken))
            {
                SubscriptionId = Settings.Default.SubscriptionId
            };

            return dnsClient;
        }

        private static KeyVaultClient CreateKeyVaultClient()
        {
            var tokenProvider = new AzureServiceTokenProvider();

            return new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(tokenProvider.KeyVaultTokenCallback));
        }

        private static IDictionary<string, string> ParseResourceId(string resourceId)
        {
            var values = resourceId.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            return new Dictionary<string, string>
            {
                { "subscriptions", values[1] },
                { "resourceGroups", values[3] },
                { "providers", values[5] }
            };
        }
    }
}