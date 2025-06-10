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
        /// Determines if a certificate should be renewed based on ARI recommendations or expiry date
        /// </summary>
        public async Task<RenewalDecision> EvaluateRenewalAsync(
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
                    decision = await EvaluateExpiryBasedRenewalAsync(certificate);
                    decision.Reason = "ARI disabled in configuration";
                    return decision;
                }

                // Check if ACME server supports ARI
                if (!acmeProtocolClient.HasRenewalInfoSupport())
                {
                    decision = await EvaluateExpiryBasedRenewalAsync(certificate);
                    decision.Reason = "ACME server does not support ARI";
                    return decision;
                }

                // Try ARI evaluation
                decision = await EvaluateAriBasedRenewalAsync(acmeProtocolClient, certificate, cancellationToken);

                return decision;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogError(ex, "Error evaluating renewal for certificate {Name}", certificate.Name);

                if (_options.AriFallbackToExpiry)
                {
                    decision = await EvaluateExpiryBasedRenewalAsync(certificate);
                    decision.Reason = $"ARI evaluation failed, fell back to expiry: {ex.Message}";
                    decision.FallbackUsed = true;
                }
                else
                {
                    decision.ShouldRenew = false;
                    decision.Reason = $"ARI evaluation failed and fallback disabled: {ex.Message}";
                }

                return decision;
            }
        }

        /// <summary>
        /// Creates an ACME order with ARI replacement support when available
        /// </summary>
        public async Task<OrderDetails> CreateOrderWithAriSupportAsync(
            AcmeProtocolClient acmeProtocolClient,
            IReadOnlyList<string> identifiers,
            KeyVaultCertificateWithPolicy existingCertificate = null,
            CancellationToken cancellationToken = default)
        {
            CertificateIdentifier existingCertId = null;

            // Extract certificate ID for replacement if available
            if (existingCertificate != null && _options.AriEnabled && acmeProtocolClient.HasRenewalInfoSupport())
            {
                existingCertId = await ExtractCertificateIdSafelyAsync(existingCertificate);
            }

            // Create order using ARI service
            var order = await CreateOrderAsync(acmeProtocolClient, identifiers, existingCertId, cancellationToken);

            return order;
        }

        /// <summary>
        /// Gets the renewal info URL for a specific certificate
        /// </summary>
        /// <param name="acmeProtocolClient">The ACME protocol client</param>
        /// <param name="certificateId">The certificate ID</param>
        /// <returns>The renewal info URL or null if not supported</returns>
        private string GetRenewalInfoEndpoint(AcmeProtocolClient acmeProtocolClient, string certificateId)
        {
            try
            {
                // Use the direct RenewalInfo property
                var baseUrl = acmeProtocolClient.Directory?.RenewalInfo;
                if (string.IsNullOrEmpty(baseUrl))
                {
                    _logger.LogDebug("ACME server does not support ARI");
                    return null;
                }

                var url = acmeProtocolClient.GetRenewalInfoUrlForCertificate(certificateId);

                _logger.LogDebug("Constructed ARI URL for certificate {CertId}: {Url}",
                    certificateId, url);

                return url;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error constructing ARI URL for certificate {CertId}", certificateId);
                return null;
            }
        }


        private async Task<RenewalInfoResponse> GetRenewalInfoAsync(string ariUrl, CancellationToken cancellationToken = default)
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

        private async Task<OrderDetails> CreateOrderAsync(AcmeProtocolClient acmeProtocolClient,
           IReadOnlyList<string> identifiers, CertificateIdentifier existingCertificateId = null,
           CancellationToken cancellationToken = default)
        {
            if (acmeProtocolClient == null)
            {
                throw new ArgumentNullException(nameof(acmeProtocolClient));
            }

            if (identifiers == null || !identifiers.Any())
            {
                throw new ArgumentException("At least one identifier is required", nameof(identifiers));
            }

            try
            {
                string replacesCertId = null;

                // If we have an existing certificate and ARI is supported, use it for replacement
                if (existingCertificateId != null && acmeProtocolClient.HasRenewalInfoSupport())
                {
                    replacesCertId = existingCertificateId.CertificateId;

                    if (AcmeProtocolClientExtensions.IsValidReplacementCertificateId(replacesCertId))
                    {
                        _logger.LogInformation("Creating ARI-aware order to replace certificate: {CertificateId}",
                            replacesCertId);
                    }
                    else
                    {
                        _logger.LogWarning("Invalid replacement certificate ID: {CertificateId}. Creating standard order.",
                            replacesCertId);
                        replacesCertId = null;
                    }
                }
                else if (existingCertificateId != null)
                {
                    _logger.LogDebug("ACME server does not support ARI. Creating standard order for renewal.");
                }
                else
                {
                    _logger.LogDebug("Creating new certificate order (no existing certificate to replace).");
                }

                // Create the order using the extension method
                var order = await acmeProtocolClient.CreateOrderWithReplacementAsync(
                    identifiers, replacesCertId, cancellationToken);

                _logger.LogInformation("Successfully created ACME order {OrderUrl} for domains: {Domains}",
                    order.OrderUrl, string.Join(", ", identifiers));

                if (!string.IsNullOrEmpty(replacesCertId))
                {
                    _logger.LogInformation("Order includes ARI replacement for certificate: {CertificateId}",
                        replacesCertId);
                }

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create ACME order for domains: {Domains}",
                    string.Join(", ", identifiers));
                throw;
            }
        }

        // Private helper methods
        private async Task<RenewalDecision> EvaluateAriBasedRenewalAsync(
            AcmeProtocolClient acmeProtocolClient,
            KeyVaultCertificateWithPolicy certificate,
            CancellationToken cancellationToken)
        {
            var decision = new RenewalDecision
            {
                CertificateName = certificate.Name,
                DecisionSource = RenewalDecisionSource.Ari
            };

            try
            {
                // Extract certificate ID for ARI
                var x509Certificate = new X509Certificate2(certificate.Cer);

                if (!CertificateIdCalculator.IsValidForAri(x509Certificate))
                {
                    var fallbackDecision = await EvaluateExpiryBasedRenewalAsync(certificate);
                    fallbackDecision.Reason = "Certificate not valid for ARI (missing required extensions)";
                    fallbackDecision.FallbackUsed = true;
                    return fallbackDecision;
                }

                var certId = CertificateIdCalculator.ExtractCertificateId(x509Certificate);
                var ariUrl = GetRenewalInfoEndpoint(acmeProtocolClient, certId.CertificateId);

                if (string.IsNullOrEmpty(ariUrl))
                {
                    var fallbackDecision = await EvaluateExpiryBasedRenewalAsync(certificate);
                    fallbackDecision.Reason = "Could not construct ARI URL";
                    fallbackDecision.FallbackUsed = true;
                    return fallbackDecision;
                }

                var renewalInfo = await GetRenewalInfoAsync(ariUrl, cancellationToken);

                if (renewalInfo == null)
                {
                    var fallbackDecision = await EvaluateExpiryBasedRenewalAsync(certificate);
                    fallbackDecision.Reason = "No ARI data available (likely new certificate)";
                    fallbackDecision.FallbackUsed = true;
                    return fallbackDecision;
                }

                if (!_renewalWindowService.IsValidRenewalWindow(renewalInfo))
                {
                    var fallbackDecision = await EvaluateExpiryBasedRenewalAsync(certificate);
                    fallbackDecision.Reason = "Invalid renewal window received from ARI";
                    fallbackDecision.FallbackUsed = true;
                    return fallbackDecision;
                }

                // ARI evaluation successful
                decision.ShouldRenew = _renewalWindowService.IsWithinRenewalWindow(renewalInfo);
                decision.AriData = renewalInfo;
                decision.Reason = _renewalWindowService.GetRenewalWindowStatus(renewalInfo);

                _logger.LogInformation("ARI renewal decision for certificate {Name}: {WindowStatus}, ShouldRenew: {ShouldRenew}",
                    certificate.Name, decision.Reason, decision.ShouldRenew);

                return decision;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogError(ex, "Error during ARI evaluation for certificate {Name}", certificate.Name);
                throw; // Let caller handle fallback
            }
        }

        private async Task<RenewalDecision> EvaluateExpiryBasedRenewalAsync(KeyVaultCertificateWithPolicy certificate)
        {
            await Task.CompletedTask; // Make it async for consistency

            var renewBeforeExpiry = TimeSpan.FromDays(_options.RenewBeforeExpiry);
            var shouldRenew = certificate.Properties.ExpiresOn < DateTimeOffset.UtcNow.Add(renewBeforeExpiry);

            _logger.LogDebug("Expiry-based renewal decision for certificate {Name}: Expires {ExpiryDate}, ShouldRenew: {ShouldRenew}",
                certificate.Name, certificate.Properties.ExpiresOn, shouldRenew);

            return new RenewalDecision
            {
                CertificateName = certificate.Name,
                ShouldRenew = shouldRenew,
                DecisionSource = RenewalDecisionSource.Expiry,
                Reason = shouldRenew
                    ? $"Certificate expires within {_options.RenewBeforeExpiry} days"
                    : $"Certificate does not expire within {_options.RenewBeforeExpiry} days",
                ExpiryDate = certificate.Properties.ExpiresOn
            };
        }

        private async Task<CertificateIdentifier> ExtractCertificateIdSafelyAsync(KeyVaultCertificateWithPolicy certificate)
        {
            try
            {
                var x509Certificate = new X509Certificate2(certificate.Cer);
                if (CertificateIdCalculator.IsValidForAri(x509Certificate))
                {
                    var certId = CertificateIdCalculator.ExtractCertificateId(x509Certificate);
                    _logger.LogDebug("Extracted certificate ID for ARI replacement: {CertificateId}", certId.CertificateId);
                    return certId;
                }
                else
                {
                    _logger.LogDebug("Certificate {Name} not valid for ARI replacement", certificate.Name);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract certificate ID for ARI replacement from certificate {Name}", certificate.Name);
                return null;
            }
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
        public bool FallbackUsed { get; set; }
        public RenewalInfoResponse AriData { get; set; }
        public DateTimeOffset? ExpiryDate { get; set; }
    }

    public enum RenewalDecisionSource
    {
        Expiry,
        Ari
    }
}
