using System.Net;
using System.Threading.Tasks;

using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace CustomDns;

public class DeleteRecord
{
    public DeleteRecord(ArmClient armClient)
    {
        _armClient = armClient;
    }

    private readonly ArmClient _armClient;

    [FunctionName(nameof(DeleteRecord))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "zones/{zoneId}/records/{recordName}")] HttpRequest req,
        string zoneId,
        string recordName,
        ILogger log)
    {
        DnsZoneResource dnsZoneResource = await _armClient.GetDnsZoneResource(ZoneIdConvert.FromZoneId(zoneId)).GetAsync();

        try
        {
            RecordSetTxtResource recordSet = await dnsZoneResource.GetRecordSetTxtAsync(recordName.Replace($".{dnsZoneResource.Data.Name}", ""));

            await recordSet.DeleteAsync(WaitUntil.Completed);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            // ignored
        }

        return new OkResult();
    }
}
