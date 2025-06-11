using System.Net;
using System.Threading.Tasks;

using DurableTask.TypedProxy;

using KeyVault.Acmebot.Internal;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Functions;

public class RenewCertificate
{
    private readonly ILogger _logger;

    public RenewCertificate(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<RenewCertificate>();
    }

    [Function($"{nameof(RenewCertificate)}_{nameof(Orchestrator)}")]
    public async Task Orchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var certificateName = context.GetInput<string>();

        var activity = context.CreateActivityProxy<ISharedActivity>();

        // 証明書の更新処理を開始
        var certificatePolicyItem = await activity.GetCertificatePolicy(certificateName);

        await context.CallSubOrchestratorAsync(nameof(SharedOrchestrator.IssueCertificate), certificatePolicyItem);
    }

    [Function($"{nameof(RenewCertificate)}_{nameof(HttpStart)}")]
    public async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/certificate/{certificateName}/renew")] HttpRequestData req,
        string certificateName,
        [DurableClient] DurableTaskClient starter)
    {
        if (!req.IsAuthenticated())
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        if (!req.HasIssueCertificateRole())
        {
            return req.CreateResponse(HttpStatusCode.Forbidden);
        }

        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(
            $"{nameof(RenewCertificate)}_{nameof(Orchestrator)}", certificateName);

        _logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");

        // Create a response that redirects to the status endpoint
        var response = req.CreateResponse(HttpStatusCode.Accepted);
        response.Headers.Add("Location", $"/api/state/{instanceId}");
        return response;
    }
}
