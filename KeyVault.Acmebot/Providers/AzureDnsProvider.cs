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
        public AzureDnsProvider(AzureDnsOptions options, IAzureEnvironment environment)
        {
            _dnsManagementClient = new DnsManagementClient(new Uri(environment.ResourceManager), new TokenCredentials(new ManagedIdentityTokenProvider(environment)))
            {
                SubscriptionId = options.SubscriptionId
            };
        }

        private readonly DnsManagementClient _dnsManagementClient;

        public int PropagationSeconds => 10;

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = await _dnsManagementClient.Zones.ListAllAsync();

            return zones.Select(x => new DnsZone { Id = x.Id, Name = x.Name }).ToArray();
        }

        public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            var resourceGroup = ExtractResourceGroup(zone.Id);

            // TXT レコードに TTL と値をセットする
            var recordSet = new RecordSet
            {
                TTL = 60,
                TxtRecords = values.Select(x => new TxtRecord(new[] { x })).ToArray()
            };

            await _dnsManagementClient.RecordSets.CreateOrUpdateAsync(resourceGroup, zone.Name, relativeRecordName, RecordType.TXT, recordSet);
        }

        public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            var resourceGroup = ExtractResourceGroup(zone.Id);

            await _dnsManagementClient.RecordSets.DeleteAsync(resourceGroup, zone.Name, relativeRecordName, RecordType.TXT);
        }

        private static string ExtractResourceGroup(string resourceId)
        {
            var values = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);

            return values[3];
        }
    }
}
