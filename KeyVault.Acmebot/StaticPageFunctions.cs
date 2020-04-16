using Azure.WebJobs.Extensions.HttpApi;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot
{
    public class StaticPageFunctions : HttpFunctionBase
    {
        public StaticPageFunctions(IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
        }

        [FunctionName(nameof(StaticPage))]
        public IActionResult StaticPage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "static-page/index")] HttpRequest req,
            ILogger log)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Forbid();
            }

            return File("index.html", "text/html");
        }
    }
}
