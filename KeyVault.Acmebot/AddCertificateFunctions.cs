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
                return ValidationProblem(ModelState);
            }

            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync(nameof(SharedFunctions.IssueCertificate), request.Domains);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return AcceptedAtFunction(nameof(AddCertificate_HttpPoll), new { instanceId }, null);
        }

        [FunctionName(nameof(AddCertificate_HttpPoll))]
        public async Task<IActionResult> AddCertificate_HttpPoll(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "add-certificate/{instanceId}")] HttpRequest req,
            string instanceId,
            [DurableClient] IDurableClient starter)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized();
            }

            var status = await starter.GetStatusAsync(instanceId);

            if (status == null)
            {
                return BadRequest();
            }

            if (status.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
            {
                return Problem(status.Output.ToString());
            }

            if (status.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                status.RuntimeStatus == OrchestrationRuntimeStatus.Pending ||
                status.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew)
            {
                return AcceptedAtFunction(nameof(AddCertificate_HttpPoll), new { instanceId }, null);
            }

            return Ok();
        }
    }
}
