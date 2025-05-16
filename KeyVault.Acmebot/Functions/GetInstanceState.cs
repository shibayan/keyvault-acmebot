using System.Net;
using System.Threading.Tasks;

using KeyVault.Acmebot.Internal;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Functions;

public class GetInstanceState
{
    private readonly ILogger _logger;

    public GetInstanceState(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<GetInstanceState>();
    }
    
    private HttpResponseData CreateProblemResponse(HttpRequestData req, string detail)
    {
        var response = req.CreateResponse(HttpStatusCode.InternalServerError);
        response.Headers.Add("Content-Type", "application/problem+json");
        response.WriteString($"{{\"detail\":\"{detail}\"}}");
        return response;
    }
    
    private HttpResponseData CreateAcceptedResponse(HttpRequestData req, string instanceId)
    {
        var response = req.CreateResponse(HttpStatusCode.Accepted);
        response.Headers.Add("Location", $"/api/state/{instanceId}");
        return response;
    }

    [Function($"{nameof(GetInstanceState)}_{nameof(HttpStart)}")]
    public async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/state/{instanceId}")] HttpRequestData req,
        string instanceId,
        [DurableClient] DurableTaskClient starter)
    {
        if (!req.IsAuthenticated())
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var status = await starter.GetInstanceAsync(instanceId);

        if (status is null)
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var response = status.RuntimeStatus switch
        {
            OrchestrationRuntimeStatus.Failed => CreateProblemResponse(req, "Orchestration failed"),
            OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending => CreateAcceptedResponse(req, instanceId),
            _ => req.CreateResponse(HttpStatusCode.OK)
        };
        
        return response;
    }
}