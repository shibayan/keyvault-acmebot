using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using KeyVault.Acmebot.Contracts;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot
{
    public class GetDnsZonesFunctions
    {
        [FunctionName(nameof(GetDnsZones))]
        public async Task<IList<string>> GetDnsZones([OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var activity = context.CreateActivityProxy<ISharedFunctions>();

            var zones = await activity.GetZones();

            return zones.Select(x => x.Name).ToArray();
        }

        [FunctionName(nameof(GetDnsZones_HttpStart))]
        public async Task<HttpResponseMessage> GetDnsZones_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "get-dns-zones")] HttpRequestMessage req,
            [OrchestrationClient] DurableOrchestrationClient starter,
            ILogger log)
        {
            if (!req.Headers.Contains("X-MS-CLIENT-PRINCIPAL-ID"))
            {
                return req.CreateErrorResponse(HttpStatusCode.Unauthorized, $"Need to activate EasyAuth.");
            }

            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(GetDnsZones), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromMinutes(1));
        }
    }
}