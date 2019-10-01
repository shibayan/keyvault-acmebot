using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using KeyVault.Acmebot.Models;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot
{
    public class AddCertificateFunctions
    {
        [FunctionName(nameof(AddCertificate_HttpStart))]
        public async Task<HttpResponseMessage> AddCertificate_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "add-certificate")] HttpRequestMessage req,
            [OrchestrationClient] DurableOrchestrationClient starter,
            ILogger log)
        {
            if (!req.Headers.Contains("X-MS-CLIENT-PRINCIPAL-ID"))
            {
                return req.CreateErrorResponse(HttpStatusCode.Unauthorized, $"Need to activate EasyAuth.");
            }

            var request = await req.Content.ReadAsAsync<AddCertificateRequest>();

            if (request?.Domains == null || request.Domains.Length == 0)
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, $"{nameof(request.Domains)} is empty.");
            }

            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync(nameof(SharedFunctions.IssueCertificate), request.Domains);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromMinutes(5));
        }
    }
}