using System.Threading.Tasks;

using Azure.WebJobs.Extensions.HttpApi;

using KeyVault.Acmebot.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot
{
    public class AddCertificateFunctions : HttpFunctionBase
    {
        public AddCertificateFunctions(IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
        }

        [FunctionName(nameof(AddCertificate_HttpStart))]
        public async Task<IActionResult> AddCertificate_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "add-certificate")] AddCertificateRequest request,
            [DurableClient] IDurableClient starter,
            ILogger log)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized();
            }

            if (!TryValidateModel(request))
            {
                return BadRequest(ModelState);
            }

            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync(nameof(SharedFunctions.IssueCertificate), request.Domains);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(Request, instanceId, true);
        }
    }
}
