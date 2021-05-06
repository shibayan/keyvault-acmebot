﻿using System.Threading.Tasks;

using Azure.Security.KeyVault.Certificates;
using Azure.WebJobs.Extensions.HttpApi;

using KeyVault.Acmebot.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Functions
{
    public class AddCertificate : HttpFunctionBase
    {
        public AddCertificate(IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
        }

        [FunctionName(nameof(AddCertificate) + "_" + nameof(HttpStart))]
        public async Task<IActionResult> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "certificate")] AddCertificateRequest request,
            [DurableClient] IDurableClient starter,
            ILogger log)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized();
            }

            if (!TryValidateModel(request))
            {
                return ValidationProblem(ModelState);
            }

            var certificateName = request.CertificateName;

            if (string.IsNullOrEmpty(certificateName))
            {
                certificateName = request.DnsNames[0].Replace("*", "wildcard").Replace(".", "-");
            }

            var subjectAlternativeNames = new SubjectAlternativeNames();

            foreach (var dnsName in request.DnsNames)
            {
                subjectAlternativeNames.DnsNames.Add(dnsName);
            }

            var certificatePolicy = new CertificatePolicy(WellKnownIssuerNames.Unknown, subjectAlternativeNames)
            {
                KeyType = request.KeyType,
                KeySize = request.KeySize,
                KeyCurveName = request.EllipticCurveName,
                ReuseKey = request.ReuseKeyOnRenewal
            };

            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync<object>(nameof(SharedOrchestrator.IssueCertificate), (certificateName, certificatePolicy));

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return AcceptedAtFunction(nameof(AddCertificate) + "_" + nameof(HttpPoll), new { instanceId }, null);
        }

        [FunctionName(nameof(AddCertificate) + "_" + nameof(HttpPoll))]
        public async Task<IActionResult> HttpPoll(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "certificate/{instanceId}")] HttpRequest req,
            string instanceId,
            [DurableClient] IDurableClient starter)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized();
            }

            var status = await starter.GetStatusAsync(instanceId);

            if (status == null)
            {
                return BadRequest();
            }

            if (status.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
            {
                return Problem(status.Output.ToString());
            }

            if (status.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                status.RuntimeStatus == OrchestrationRuntimeStatus.Pending ||
                status.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew)
            {
                return AcceptedAtFunction(nameof(AddCertificate) + "_" + nameof(HttpPoll), new { instanceId }, null);
            }

            return Ok();
        }
    }
}
