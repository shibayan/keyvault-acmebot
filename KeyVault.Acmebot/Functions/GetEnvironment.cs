using System;
using System.Collections.Generic;
using System.IO;

using Azure.WebJobs.Extensions.HttpApi;

using KeyVault.Acmebot.Options;
using KeyVault.Acmebot.Providers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Functions;

public class GetEnvironment : HttpFunctionBase
{
    public GetEnvironment(IHttpContextAccessor httpContextAccessor, IEnumerable<IDnsProvider> dnsProviders, IOptions<AcmebotOptions> options)
        : base(httpContextAccessor)
    {
        _dnsProviders = dnsProviders;
        _options = options.Value;
    }

    private readonly IEnumerable<IDnsProvider> _dnsProviders;
    private readonly AcmebotOptions _options;

    [FunctionName($"{nameof(GetEnvironment)}_{nameof(HttpStart)}")]
    public IActionResult HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/environment")] HttpRequest req,
        ILogger log)
    {
        if (!User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }

        SaveState(new { text = "test" }, "sample.json");

        var content = new
        {
            endpoint = _options.Endpoint,
            environment = _options.Environment,
            contacts = _options.Contacts,
            statePath = Environment.ExpandEnvironmentVariables($"%HOME%/data/.acmebot/"),
            content = string.Join("\n", System.IO.Directory.GetDirectories(Environment.ExpandEnvironmentVariables($"%HOME%/data/")))
        };

        return new OkObjectResult(content);
    }

    private void SaveState<TState>(TState value, string path)
    {
        var fullPath = ResolveStateFullPath(path);
        var directoryPath = Path.GetDirectoryName(fullPath);

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonConvert.SerializeObject(value, Formatting.Indented);

        System.IO.File.WriteAllText(fullPath, json);
    }

    private string ResolveStateFullPath(string path) => Environment.ExpandEnvironmentVariables($"%HOME%/data/.acmebot/{_options.Endpoint.Host}/{path}");
}
