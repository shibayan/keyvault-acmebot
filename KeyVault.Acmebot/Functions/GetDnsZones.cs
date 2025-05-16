using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

using DurableTask.TypedProxy;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Models;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Functions;

public class GetDnsZones
{
    private readonly ILogger _logger;

    public GetDnsZones(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<GetDnsZones>();
    }

    [Function($"{nameof(GetDnsZones)}_{nameof(Orchestrator)}")]
    public Task<IReadOnlyList<DnsZoneGroup>> Orchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var activity = context.CreateActivityProxy<ISharedActivity>();

        return activity.GetAllDnsZones();
    }

    [Function($"{nameof(GetDnsZones)}_{nameof(HttpStart)}")]
    public async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/dns-zones")] HttpRequestData req,
        [DurableClient] DurableTaskClient starter)
    {
        if (!req.IsAuthenticated())
        {
            var response = req.CreateResponse(HttpStatusCode.Unauthorized);
            return response;
        }

        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(
            $"{nameof(GetDnsZones)}_{nameof(Orchestrator)}");

        _logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");

        return await starter.CreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromMinutes(1));
    }
}
