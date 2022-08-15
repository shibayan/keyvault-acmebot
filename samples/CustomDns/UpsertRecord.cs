﻿using System.Threading.Tasks;

using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;

using CustomDns.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace CustomDns;

public class UpsertRecord
{
    public UpsertRecord(ArmClient armClient)
    {
        _armClient = armClient;
    }

    private readonly ArmClient _armClient;

    [FunctionName(nameof(UpsertRecord))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "zones/{zoneId}/records/{recordName}")] UpsertRecordRequest model,
        string zoneId,
        string recordName,
        ILogger log)
    {
        var recordSet = new TxtRecordSetData
        {
            TTL = model.TTL
        };

        foreach (var value in model.Values)
        {
            recordSet.TxtRecords.Add(new TxtRecord { Value = { value } });
        }

        var dnsZoneResource = _armClient.GetDnsZoneResource(new ResourceIdentifier(zoneId));

        var collection = dnsZoneResource.GetRecordSetTxts();

        await collection.CreateOrUpdateAsync(WaitUntil.Completed, recordName, recordSet);

        return new OkResult();
    }
}
