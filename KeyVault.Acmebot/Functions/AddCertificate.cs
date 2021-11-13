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
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/certificate")] CertificatePolicyItem certificatePolicyItem,
            [DurableClient] IDurableClient starter,
            ILogger log)
        {
            if (!User.IsAppAuthorized())
            {
                return Unauthorized();
            }

            if (!TryValidateModel(certificatePolicyItem))
            {
                return ValidationProblem(ModelState);
            }

            if (string.IsNullOrEmpty(certificatePolicyItem.CertificateName))
            {
                certificatePolicyItem.CertificateName = certificatePolicyItem.DnsNames[0].Replace("*", "wildcard").Replace(".", "-");
            }

            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync(nameof(SharedOrchestrator.IssueCertificate), certificatePolicyItem);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return AcceptedAtFunction(nameof(GetInstanceState) + "_" + nameof(GetInstanceState.HttpStart), new { instanceId }, null);
        }
    }
}
