using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Azure.WebJobs.Extensions.HttpApi;

using DurableTask.TypedProxy;

using KeyVault.Acmebot.Contracts;
using KeyVault.Acmebot.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot
{
    public class GetCertificatesFunctions : HttpFunctionBase
    {
        public GetCertificatesFunctions(IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
        }

        [FunctionName(nameof(GetCertificates))]
        public async Task<IList<GetCertificateResponse>> GetCertificates([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var activity = context.CreateActivityProxy<ISharedFunctions>();

            var certificates = await activity.GetAllCertificates();

            return certificates.Select(x => new GetCertificateResponse(x))
                               .ToArray();
        }

        [FunctionName(nameof(GetCertificates_HttpStart))]
        public async Task<IActionResult> GetCertificates_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "get-certificates")] HttpRequest req,
            [DurableClient] IDurableClient starter,
            ILogger log)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized();
            }

            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(GetCertificates), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromMinutes(1));
        }
    }
}
