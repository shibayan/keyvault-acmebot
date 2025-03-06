using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.PrivateDns;
using Azure.ResourceManager.PrivateDns.Models;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;

namespace KeyVault.Acmebot.Providers;

internal class AzurePrivateDnsProvider : IDnsProvider
{
    public AzurePrivateDnsProvider(AzurePrivateDnsOptions options, AzureEnvironment environment, TokenCredential credential)
    {
        _armClient = new ArmClient(credential, options.SubscriptionId, new ArmClientOptions { Environment = environment.ResourceManager });
    }

    private readonly ArmClient _armClient;

    public string Name => "Azure Private DNS";

    public int PropagationSeconds => 10;

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var zones = new List<DnsZone>();

        var subscription = await _armClient.GetDefaultSubscriptionAsync();

        var result = subscription.GetPrivateDnsZonesAsync();

        await foreach (var zone in result)
        {
            zones.Add(new DnsZone(this) { Id = zone.Id, Name = zone.Data.Name });
        }

        return zones;
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        // TXT レコードに値をセットする
        var txtRecordData = new PrivateDnsTxtRecordData() { TtlInSeconds = 3600 };

        foreach (var value in values)
        {
            txtRecordData.PrivateDnsTxtRecords.Add(new PrivateDnsTxtRecordInfo { Values = { value } });
        }

        var dnsZoneResource = _armClient.GetPrivateDnsZoneResource(new ResourceIdentifier(zone.Id));

        var dnsTxtRecords = dnsZoneResource.GetPrivateDnsTxtRecords();

        return dnsTxtRecords.CreateOrUpdateAsync(WaitUntil.Completed, relativeRecordName, txtRecordData);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        var dnsZoneResource = _armClient.GetPrivateDnsZoneResource(new ResourceIdentifier(zone.Id));

        try
        {
            PrivateDnsTxtRecordResource dnsTxtRecordResource = await dnsZoneResource.GetPrivateDnsTxtRecordAsync(relativeRecordName);

            await dnsTxtRecordResource.DeleteAsync(WaitUntil.Completed);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            // ignored
        }
    }
}
