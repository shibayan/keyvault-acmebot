using System.Net;

using KeyVault.Acmebot.Internal;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Functions;

public class StaticPage
{
    private readonly ILogger _logger;

    public StaticPage(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<StaticPage>();
    }

    [Function($"{nameof(StaticPage)}_{nameof(Serve)}")]
    public HttpResponseData Serve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{*path}")] HttpRequestData req)
    {
        if (!req.IsAuthenticated())
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        string path = req.Url.LocalPath.TrimStart('/');
        return StaticFileHelper.ServeStaticFile(req, path);
    }
}
