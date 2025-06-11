using System.Net;
using System.Threading.Tasks;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Models;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Functions;

public class AddCertificate
{
    private readonly ILogger _logger;

    public AddCertificate(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<AddCertificate>();
    }

    [Function($"{nameof(AddCertificate)}_{nameof(HttpStart)}")]
    public async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/certificate")] HttpRequestData req,
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

        // Parse the JSON input
        CertificatePolicyItem certificatePolicyItem;
        try
        {
            certificatePolicyItem = await req.ReadFromJsonAsync<CertificatePolicyItem>();
            
            if (certificatePolicyItem == null)
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                response.WriteString("Invalid certificate policy data");
                return response;
            }
        }
        catch
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            response.WriteString("Failed to parse request body as CertificatePolicyItem");
            return response;
        }

        if (string.IsNullOrEmpty(certificatePolicyItem.CertificateName))
        {
            certificatePolicyItem.CertificateName = certificatePolicyItem.DnsNames[0].Replace("*", "wildcard").Replace(".", "-");
        }

        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(
            nameof(SharedOrchestrator.IssueCertificate), certificatePolicyItem);

        _logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");

        // Create a response that redirects to the status endpoint
        var response = req.CreateResponse(HttpStatusCode.Accepted);
        response.Headers.Add("Location", $"/api/instance/{instanceId}");
        return response;
    }
}
