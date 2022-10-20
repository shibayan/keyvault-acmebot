using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.PrivateDns;
using Azure.ResourceManager.PrivateDns.Models;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;

namespace KeyVault.Acmebot.Providers;

internal class AzurePrivateDnsProvider : IDnsProvider
{
    public AzurePrivateDnsProvider(AzurePrivateDnsOptions options, AzureEnvironment environment)
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

        var result = subscription.GetPrivateZonesAsync();

        await foreach (var zone in result)
        {
            zones.Add(new DnsZone(this) { Id = zone.Id, Name = zone.Data.Name });
        }

        return zones;
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        // TXT レコードに値をセットする
        var recordSet = new RecordSetData();

        foreach (var value in values)
        {
            recordSet.TxtRecords.Add(new TxtRecord { Value = { value } });
        }

        var dnsZoneResource = _armClient.GetPrivateZoneResource(new ResourceIdentifier(zone.Id));

        var recordSets = dnsZoneResource.GetRecordSets();

        return recordSets.CreateOrUpdateAsync(WaitUntil.Completed, relativeRecordName, recordSet);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        var dnsZoneResource = _armClient.GetPrivateZoneResource(new ResourceIdentifier(zone.Id));

        try
        {
            var recordSet = await dnsZoneResource.GetRecordSets().GetAsync(relativeRecordName);

            await recordSet.Value.DeleteAsync(WaitUntil.Completed);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            // ignored
        }
    }
}
