using System;
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

public class RevokeCertificate
{
    private readonly ILogger _logger;

    public RevokeCertificate(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<RevokeCertificate>();
    }

    [Function($"{nameof(RevokeCertificate)}_{nameof(Orchestrator)}")]
    public async Task Orchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var certificateName = context.GetInput<string>();

        var activity = context.CreateActivityProxy<ISharedActivity>();

        await activity.RevokeCertificate(certificateName);
    }

    [Function($"{nameof(RevokeCertificate)}_{nameof(HttpStart)}")]
    public async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/certificate/{certificateName}/revoke")] HttpRequestData req,
        string certificateName,
        [DurableClient] DurableTaskClient starter)
    {
        if (!req.IsAuthenticated())
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        if (!req.HasRevokeCertificateRole())
        {
            return req.CreateResponse(HttpStatusCode.Forbidden);
        }

        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(
            $"{nameof(RevokeCertificate)}_{nameof(Orchestrator)}", certificateName);

        _logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");

        return await starter.CreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromMinutes(1));
    }
}
