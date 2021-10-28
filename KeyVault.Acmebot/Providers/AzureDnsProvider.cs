using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Azure.Identity;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;

namespace KeyVault.Acmebot.Providers
{
    public class AzureDnsProvider : IDnsProvider
    {
        public AzureDnsProvider(AzureDnsOptions options, AzureEnvironment environment)
        {
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                AuthorityHost = environment.ActiveDirectory
            });

            _dnsManagementClient = new DnsManagementClient(options.SubscriptionId, environment.ResourceManager, credential);
        }

        private readonly DnsManagementClient _dnsManagementClient;

        public int PropagationSeconds => 10;

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = new List<DnsZone>();

            var result = _dnsManagementClient.Zones.ListAsync();

            await foreach (var zone in result)
            {
                zones.Add(new DnsZone { Id = zone.Id, Name = zone.Name, NameServers = zone.NameServers });
            }

            return zones;
        }

        public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            var resourceGroup = ExtractResourceGroup(zone.Id);

            // TXT レコードに TTL と値をセットする
            var recordSet = new RecordSet
            {
                TTL = 60
            };

            foreach (var value in values)
            {
                recordSet.TxtRecords.Add(new TxtRecord { Value = { value } });
            }

            return _dnsManagementClient.RecordSets.CreateOrUpdateAsync(resourceGroup, zone.Name, relativeRecordName, RecordType.TXT, recordSet);
        }

        public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            var resourceGroup = ExtractResourceGroup(zone.Id);

            return _dnsManagementClient.RecordSets.DeleteAsync(resourceGroup, zone.Name, relativeRecordName, RecordType.TXT);
        }

        private static string ExtractResourceGroup(string resourceId)
        {
            var values = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);

            return values[3];
        }
    }
}
