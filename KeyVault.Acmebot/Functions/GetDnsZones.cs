﻿using Azure.Functions.Worker.Extensions.HttpApi;

using KeyVault.Acmebot.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Functions;

public class GetDnsZones(IHttpContextAccessor httpContextAccessor, ILogger<GetDnsZones> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(GetDnsZones)}_{nameof(Orchestrator)}")]
    public Task<IReadOnlyList<DnsZoneGroup>> Orchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return context.CallGetAllDnsZonesAsync(null!);
    }

    [Function($"{nameof(GetDnsZones)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/dns-zones")] HttpRequest req,
        [DurableClient] DurableTaskClient starter)
    {
        if (!User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }

        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync($"{nameof(GetDnsZones)}_{nameof(Orchestrator)}");

        logger.LogInformation("Started orchestration with ID = '{InstanceId}'.", instanceId);

        var metadata = await starter.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);

        return Ok(metadata.SerializedOutput);
    }
}
