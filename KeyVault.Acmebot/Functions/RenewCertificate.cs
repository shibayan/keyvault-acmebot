using System.Threading.Tasks;

using Azure.WebJobs.Extensions.HttpApi;

using DurableTask.TypedProxy;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Functions
{
    public class RenewCertificate : HttpFunctionBase
    {
        public RenewCertificate(IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
        }

        [FunctionName(nameof(RenewCertificate) + "_" + nameof(Orchestrator))]
        public async Task Orchestrator([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var certificateName = context.GetInput<string>();

            var activity = context.CreateActivityProxy<ISharedActivity>();

            // 証明書の更新処理を開始
            var certificatePolicyItem = await activity.GetCertificatePolicy(certificateName);

            await context.CallSubOrchestratorAsync(nameof(SharedOrchestrator.IssueCertificate), certificatePolicyItem);
        }

        [FunctionName(nameof(RenewCertificate) + "_" + nameof(HttpStart))]
        public async Task<IActionResult> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/certificate/{certificateName}/renew")] HttpRequest req,
            string certificateName,
            [DurableClient] IDurableClient starter,
            ILogger log)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized();
            }

            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync(nameof(RenewCertificate) + "_" + nameof(Orchestrator), null, certificateName);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return AcceptedAtFunction(nameof(GetInstanceState) + "_" + nameof(GetInstanceState.HttpStart), new { instanceId }, null);
        }
    }
}
