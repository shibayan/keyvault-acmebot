using System.Threading.Tasks;

using Azure.WebJobs.Extensions.HttpApi;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Functions
{
    public class AddCertificate : HttpFunctionBase
    {
        public AddCertificate(IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
        }

        [FunctionName(nameof(AddCertificate) + "_" + nameof(HttpStart))]
        public async Task<IActionResult> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "certificate")] AddCertificateRequest request,
            [DurableClient] IDurableClient starter,
            ILogger log)
        {
            if (!User.IsAppAuthorized())
            {
                return Unauthorized();
            }

            if (!TryValidateModel(request))
            {
                return ValidationProblem(ModelState);
            }

            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync(nameof(SharedOrchestrator.IssueCertificate), request.DnsNames);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return AcceptedAtFunction(nameof(AddCertificate) + "_" + nameof(HttpPoll), new { instanceId }, null);
        }

        [FunctionName(nameof(AddCertificate) + "_" + nameof(HttpPoll))]
        public async Task<IActionResult> HttpPoll(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "certificate/{instanceId}")] HttpRequest req,
            string instanceId,
            [DurableClient] IDurableClient starter)
        {
            if (!User.IsAppAuthorized())
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
                return AcceptedAtFunction(nameof(AddCertificate) + "_" + nameof(HttpPoll), new { instanceId }, null);
            }

            return Ok();
        }
    }
}
