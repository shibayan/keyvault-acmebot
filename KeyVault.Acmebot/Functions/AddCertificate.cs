﻿using System.Threading.Tasks;

using Azure.Functions.Worker.Extensions.HttpApi;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace KeyVault.Acmebot.Functions;

public class AddCertificate(IHttpContextAccessor httpContextAccessor, ILogger<AddCertificate> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(AddCertificate)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/certificate")] HttpRequest req,
        [FromBody] CertificatePolicyItem certificatePolicyItem,
        [DurableClient] DurableTaskClient starter)
    {
        if (!User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }

        if (!User.HasIssueCertificateRole())
        {
            return Forbid();
        }

        if (!TryValidateModel(certificatePolicyItem))
        {
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrEmpty(certificatePolicyItem.CertificateName))
        {
            certificatePolicyItem.CertificateName = certificatePolicyItem.DnsNames[0].Replace("*", "wildcard").Replace(".", "-");
        }

        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(nameof(SharedOrchestrator.IssueCertificate), certificatePolicyItem);

        logger.LogInformation("Started orchestration with ID = '{InstanceId}'.", instanceId);

        return AcceptedAtFunction($"{nameof(GetInstanceState)}_{nameof(GetInstanceState.HttpStart)}", new { instanceId }, null);
    }
}
