using System.Collections.Generic;
using System.Threading.Tasks;

using Azure.ResourceManager;
using Azure.ResourceManager.Dns;

using CustomDns.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace CustomDns;

public class ListZones
{
    public ListZones(ArmClient armClient)
    {
        _armClient = armClient;
    }

    private readonly ArmClient _armClient;

    [FunctionName(nameof(ListZones))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "zones")] HttpRequest req,
        ILogger log)
    {
        var subscription = await _armClient.GetDefaultSubscriptionAsync();

        var result = new List<ListZoneResult>();

        await foreach (var dnsZone in subscription.GetDnsZonesByDnszoneAsync())
        {
            result.Add(new ListZoneResult { Id = ZoneIdConvert.ToZoneId(dnsZone.Id), Name = dnsZone.Data.Name, NameServers = dnsZone.Data.NameServers });
        }

        return new OkObjectResult(result);
    }
}
