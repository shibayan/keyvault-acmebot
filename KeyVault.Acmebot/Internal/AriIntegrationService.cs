using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;

using Azure.Security.KeyVault.Certificates;

using KeyVault.Acmebot.Models;
using KeyVault.Acmebot.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KeyVault.Acmebot.Internal
{
    /// <summary>
    /// Service that integrates ARI (ACME Renewal Information) into certificate renewal workflows
    /// </summary>
    public class AriIntegrationService
    {
        private readonly ILogger<AriIntegrationService> _logger;
        private readonly AcmebotOptions _options;
        private readonly HttpClient _httpClient;
        private readonly RenewalWindowService _renewalWindowService;

        public AriIntegrationService(
            ILogger<AriIntegrationService> logger,
            IOptions<AcmebotOptions> options,
            HttpClient httpClient,
            RenewalWindowService renewalWindowService
            )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _renewalWindowService = renewalWindowService ?? throw new ArgumentNullException(nameof(renewalWindowService));
        }

        /// <summary>
        /// Evaluates a certificate for renewal using ARI or expiry-based logic (migrated from SharedActivity)
        /// </summary>
        /// <param name="certificate">Certificate properties</param>
        /// <param name="currentDateTime">Current date/time for expiry calculations</param>
        /// <param name="acmeProtocolClient">ACME client for ARI operations</param>
        /// <param name="certificateClient">Certificate client for retrieving full certificate data</param>
        /// <returns>CertificateItem if renewal is needed, null otherwise</returns>
        public async Task<CertificateItem> IsCertificateDueForRenewal(
            CertificateProperties certificate,
            DateTime currentDateTime,
            AcmeProtocolClient acmeProtocolClient,
            CertificateClient certificateClient)
        {
            var fullCertificate = await certificateClient.GetCertificateAsync(certificate.Name);
            try
            {

                var renewalDecision = await GetARIRenewalDecisionAsync(
                     acmeProtocolClient, fullCertificate, CancellationToken.None);

                _logger.LogDebug("ARI renewal decision for certificate {Name}: {ShouldRenew} ({DecisionSource}) - {Reason}",
                    certificate.Name, renewalDecision.ShouldRenew, renewalDecision.DecisionSource, renewalDecision.Reason);

                if (renewalDecision.ShouldRenew && renewalDecision.DecisionSource == RenewalDecisionSource.Ari)
                {
                    var certificateItem = fullCertificate.Value.ToCertificateItem();
                    certificateItem.AriDecisionSource = renewalDecision.DecisionSource.ToString();
                    certificateItem.AriReason = renewalDecision.Reason;
                    return certificateItem;
                }

                // Fall back to expiry-based evaluation
                return GetExpiryBasedRenewal(certificate, currentDateTime, fullCertificate.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating certificate {Name} for renewal", certificate.Name);
                return GetExpiryBasedRenewal(certificate, currentDateTime, fullCertificate.Value);
            }
        }

        /// <summary>
        /// Determines if a certificate should be renewed based on ARI recommendations or expiry date
        /// </summary>
        private async Task<RenewalDecision> GetARIRenewalDecisionAsync(
            AcmeProtocolClient acmeProtocolClient,
            KeyVaultCertificateWithPolicy certificate,
            CancellationToken cancellationToken = default)
        {
            var decision = new RenewalDecision
            {
                CertificateName = certificate.Name,
                ShouldRenew = false,
                DecisionSource = RenewalDecisionSource.Expiry,
                Reason = "Not evaluated"
            };

            try
            {
                // Check ARI configuration
                if (!_options.AriEnabled)
                {
                    decision.Reason = "ARI disabled in configuration";
                    return decision;
                }

                // Check if ACME server supports ARI
                if (!acmeProtocolClient.SupportsARI())
                {
                    decision.Reason = "ACME server does not support ARI";
                    return decision;
                }


                var x509Certificate = new X509Certificate2(certificate.Cer);
                var certId = CertificateIdCalculator.ExtractCertificateId(x509Certificate);
                var ariUrl = acmeProtocolClient.GetRenewalInfoUrlForCertificate(certId.CertificateId);

                if (!CertificateIdCalculator.IsValidForAri(x509Certificate))
                {
                    decision.Reason = "Certificate not valid for ARI or ARI URL is empty";
                    _logger.LogDebug("Certificate {Name} is not valid for ARI or ARI URL is empty", certificate.Name);
                    return decision;
                }

                if (string.IsNullOrEmpty(ariUrl))
                {
                    decision.Reason = "ARI URL is null or empty";
                    _logger.LogDebug("ARI URL for certificate {Name} is null or empty", certificate.Name);
                    return decision;
                }

                var renewalInfo = await GetARIResponse(ariUrl, cancellationToken);

                if (renewalInfo == null)
                {
                    decision.Reason = "No renewal info available from ARI";
                    _logger.LogDebug("No renewal info available from ARI for certificate {Name}", certificate.Name);
                    return decision;
                }

                if (!_renewalWindowService.IsValidRenewalWindow(renewalInfo))
                {
                    decision.Reason = "Invalid renewal window data from ARI";
                    _logger.LogDebug("Invalid renewal window data from ARI for certificate {Name}", certificate.Name);
                    return decision;
                }

                // ARI evaluation successful
                decision.ShouldRenew = _renewalWindowService.IsWithinRenewalWindow(renewalInfo);
                decision.AriData = renewalInfo;
                decision.Reason = _renewalWindowService.GetRenewalWindowStatus(renewalInfo);
                decision.DecisionSource = RenewalDecisionSource.Ari;

                _logger.LogInformation("ARI renewal decision for certificate {Name}: {WindowStatus}, ShouldRenew: {ShouldRenew}",
                    certificate.Name, decision.Reason, decision.ShouldRenew);

                return decision;

            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogError(ex, "Error evaluating ARI renewal for certificate {Name}", certificate.Name);
                throw;
            }
        }

        private async Task<RenewalInfoResponse> GetARIResponse(string ariUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(ariUrl))
            {
                _logger.LogWarning("ARI URL is null or empty");
                return null;
            }

            try
            {
                _logger.LogDebug("Requesting renewal info from ARI endpoint: {AriUrl}", ariUrl);

                using var response = await _httpClient.GetAsync(ariUrl, cancellationToken);

                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        var renewalInfo = await response.Content.ReadFromJsonAsync<RenewalInfoResponse>(cancellationToken: cancellationToken);

                        if (renewalInfo?.SuggestedWindow != null)
                        {
                            _logger.LogInformation("Successfully retrieved ARI data. Renewal window: {Start} to {End}",
                                renewalInfo.SuggestedWindow.Start, renewalInfo.SuggestedWindow.End);
                            return renewalInfo;
                        }

                        _logger.LogWarning("ARI response missing required SuggestedWindow data");
                        return null;

                    case HttpStatusCode.NotFound:
                        _logger.LogInformation("Certificate not found in ARI system (404). This is normal for new certificates.");
                        return null;

                    case HttpStatusCode.BadRequest:
                        _logger.LogError("Bad request to ARI endpoint. Certificate ID may be invalid: {AriUrl}", ariUrl);
                        return null;

                    default:
                        _logger.LogError("Unexpected HTTP status code from ARI endpoint: {StatusCode}", response.StatusCode);
                        return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during ARI request to {AriUrl}", ariUrl);
                return null;
            }
        }



        /// <summary>
        /// Evaluates certificate renewal based on expiry date (migrated from SharedActivity)
        /// </summary>
        /// <param name="certificate">Certificate properties</param>
        /// <param name="currentDateTime">Current date/time</param>
        /// <param name="fullCertificate">Full certificate with policy</param>
        /// <returns>CertificateItem if renewal needed, null otherwise</returns>
        private CertificateItem GetExpiryBasedRenewal(
            CertificateProperties certificate,
            DateTime currentDateTime,
            KeyVaultCertificateWithPolicy fullCertificate)
        {
            var shouldRenew = (certificate.ExpiresOn.Value - currentDateTime).TotalDays <= _options.RenewBeforeExpiry;

            if (shouldRenew)
            {
                _logger.LogDebug("Expiry-based renewal needed for certificate {Name}: expires {ExpiryDate}",
                    certificate.Name, certificate.ExpiresOn);
                return fullCertificate.ToCertificateItem();
            }

            return null;
        }


    }

    /// <summary>
    /// Represents the result of a certificate renewal evaluation
    /// </summary>
    public class RenewalDecision
    {
        public string CertificateName { get; set; }
        public bool ShouldRenew { get; set; }
        public RenewalDecisionSource DecisionSource { get; set; }
        public string Reason { get; set; }
        public RenewalInfoResponse AriData { get; set; }
    }

    public enum RenewalDecisionSource
    {
        Expiry,
        Ari
    }
}
