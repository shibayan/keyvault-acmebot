using System.Threading.Tasks;

using Azure.WebJobs.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace KeyVault.Acmebot.Functions;

public class GetInstanceState : HttpFunctionBase
{
    public GetInstanceState(IHttpContextAccessor httpContextAccessor)
        : base(httpContextAccessor)
    {
    }

    [FunctionName($"{nameof(GetInstanceState)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/state/{instanceId}")] HttpRequest req,
        string instanceId,
        [DurableClient] IDurableClient starter)
    {
        if (!User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }

        var status = await starter.GetStatusAsync(instanceId);

        if (status is null)
        {
            return BadRequest();
        }

        if (status.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
        {
            return Problem(status.Output.ToString());
        }

        if (status.RuntimeStatus is OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending or OrchestrationRuntimeStatus.ContinuedAsNew)
        {
            return AcceptedAtFunction($"{nameof(GetInstanceState)}_{nameof(HttpStart)}", new { instanceId }, null);
        }

        return Ok();
    }
}
