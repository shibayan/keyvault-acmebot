using System.Collections.Generic;
using System.Threading.Tasks;

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;

namespace KeyVault.Acmebot.Providers;

public class AzureDnsProvider : IDnsProvider
{
    public AzureDnsProvider(AzureDnsOptions options, AzureEnvironment environment)
    {
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            AuthorityHost = environment.ActiveDirectory
        });

        _armClient = new ArmClient(credential, options.SubscriptionId, new ArmClientOptions { Environment = environment.ResourceManager });
    }

    private readonly ArmClient _armClient;

    public int PropagationSeconds => 10;

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var zones = new List<DnsZone>();

        var subscription = await _armClient.GetDefaultSubscriptionAsync();

        var result = subscription.GetDnsZonesByDnszoneAsync();

        await foreach (var zone in result)
        {
            zones.Add(new DnsZone(this) { Id = zone.Id, Name = zone.Data.Name, NameServers = zone.Data.NameServers });
        }

        return zones;
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        // TXT レコードに TTL と値をセットする
        var recordSet = new TxtRecordSetData
        {
            TTL = 60
        };

        foreach (var value in values)
        {
            recordSet.TxtRecords.Add(new TxtRecord { Value = { value } });
        }

        var dnsZoneResource = _armClient.GetDnsZoneResource(new ResourceIdentifier(zone.Id));

        var recordSets = dnsZoneResource.GetRecordSetTxts();

        return recordSets.CreateOrUpdateAsync(WaitUntil.Completed, relativeRecordName, recordSet);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        var dnsZoneResource = _armClient.GetDnsZoneResource(new ResourceIdentifier(zone.Id));

        var recordSets = await dnsZoneResource.GetRecordSetTxtAsync(relativeRecordName);

        await recordSets.Value.DeleteAsync(WaitUntil.Completed);
    }
}
