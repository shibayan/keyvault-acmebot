using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly AriClient _ariClient;
        private readonly AriDirectoryService _ariDirectoryService;
        private readonly RenewalWindowService _renewalWindowService;
        private readonly AriOrderService _ariOrderService;

        public AriIntegrationService(
            ILogger<AriIntegrationService> logger,
            IOptions<AcmebotOptions> options,
            AriClient ariClient,
            AriDirectoryService ariDirectoryService,
            RenewalWindowService renewalWindowService,
            AriOrderService ariOrderService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _ariClient = ariClient ?? throw new ArgumentNullException(nameof(ariClient));
            _ariDirectoryService = ariDirectoryService ?? throw new ArgumentNullException(nameof(ariDirectoryService));
            _renewalWindowService = renewalWindowService ?? throw new ArgumentNullException(nameof(renewalWindowService));
            _ariOrderService = ariOrderService ?? throw new ArgumentNullException(nameof(ariOrderService));
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
            var order = await _ariOrderService.CreateOrderAsync(acmeProtocolClient, identifiers, existingCertId, cancellationToken);
            
            // Log order creation details
            _ariOrderService.LogOrderCreation(order, identifiers, existingCertId != null);
            
            return order;
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
                var ariUrl = _ariDirectoryService.GetRenewalInfoEndpoint(acmeProtocolClient, certId.CertificateId);
                
                if (string.IsNullOrEmpty(ariUrl))
                {
                    var fallbackDecision = await EvaluateExpiryBasedRenewalAsync(certificate);
                    fallbackDecision.Reason = "Could not construct ARI URL";
                    fallbackDecision.FallbackUsed = true;
                    return fallbackDecision;
                }

                var renewalInfo = await _ariClient.GetRenewalInfoAsync(ariUrl, cancellationToken);
                
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
