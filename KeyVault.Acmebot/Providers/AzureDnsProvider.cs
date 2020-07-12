using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;

using Microsoft.Azure.Management.Dns;
using Microsoft.Azure.Management.Dns.Models;
using Microsoft.Rest;

namespace KeyVault.Acmebot.Providers
{
    public class AzureDnsProvider : IDnsProvider
    {
        public AzureDnsProvider(AcmebotOptions options)
        {
            _dnsManagementClient = new DnsManagementClient(new TokenCredentials(new AppAuthenticationTokenProvider()))
            {
                SubscriptionId = options.AzureDns?.SubscriptionId ?? options.SubscriptionId
            };
        }

        private readonly DnsManagementClient _dnsManagementClient;

        public async Task<IList<DnsZone>> ListZonesAsync()
        {
            var zones = await _dnsManagementClient.Zones.ListAllAsync();

            return zones.Select(x => new DnsZone { Id = x.Id, Name = x.Name }).ToArray();
        }

        public async Task UpsertTxtRecordAsync(DnsZone zone, string recordName, IEnumerable<string> values)
        {
            var resourceGroup = ExtractResourceGroup(zone.Id);

            // 既存の TXT レコードがあれば取得する
            var recordSet = await _dnsManagementClient.RecordSets.GetOrDefaultAsync(resourceGroup, zone.Name, recordName, RecordType.TXT) ?? new RecordSet();

            // TXT レコードに TTL と値をセットする
            recordSet.TTL = 60;
            recordSet.TxtRecords = values.Select(x => new TxtRecord(new[] { x })).ToArray();

            await _dnsManagementClient.RecordSets.CreateOrUpdateAsync(resourceGroup, zone.Name, recordName, RecordType.TXT, recordSet);
        }

        private static string ExtractResourceGroup(string resourceId)
        {
            var values = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);

            return values[3];
        }
    }
}
