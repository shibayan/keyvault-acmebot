using Azure.WebJobs.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Functions;

public class StaticPage : HttpFunctionBase
{
    public StaticPage(IHttpContextAccessor httpContextAccessor)
        : base(httpContextAccessor)
    {
    }

    [FunctionName($"{nameof(StaticPage)}_{nameof(Serve)}")]
    public IActionResult Serve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{*path}")] HttpRequest req,
        ILogger log)
    {
        if (!IsAuthenticationEnabled || !User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }

        return LocalStaticApp();
    }
}
