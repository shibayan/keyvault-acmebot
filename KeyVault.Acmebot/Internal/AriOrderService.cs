using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;
using KeyVault.Acmebot.Models;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Internal
{
    /// <summary>
    /// Service for creating ACME orders with ARI (ACME Renewal Information) support
    /// </summary>
    public class AriOrderService
    {
        private readonly ILogger<AriOrderService> _logger;

        public AriOrderService(ILogger<AriOrderService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates an ACME order with optional certificate replacement for ARI
        /// </summary>
        /// <param name="acmeProtocolClient">The ACME protocol client</param>
        /// <param name="identifiers">Domain identifiers for the certificate</param>
        /// <param name="existingCertificateId">Certificate ID of the certificate being replaced (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Order details</returns>
        public async Task<OrderDetails> CreateOrderAsync(AcmeProtocolClient acmeProtocolClient,
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

        /// <summary>
        /// Determines if an order should include certificate replacement based on ARI recommendations
        /// </summary>
        /// <param name="acmeProtocolClient">The ACME protocol client</param>
        /// <param name="renewalInfo">ARI renewal information</param>
        /// <param name="existingCertificateId">Existing certificate identifier</param>
        /// <returns>True if the order should include replacement</returns>
        public bool ShouldIncludeReplacement(AcmeProtocolClient acmeProtocolClient,
            RenewalInfoResponse renewalInfo, CertificateIdentifier existingCertificateId)
        {
            if (acmeProtocolClient == null || existingCertificateId == null)
            {
                return false;
            }

            // Only include replacement if server supports ARI
            if (!acmeProtocolClient.HasRenewalInfoSupport())
            {
                _logger.LogDebug("ACME server does not support ARI - no replacement will be included");
                return false;
            }

            // Validate certificate ID
            if (!AcmeProtocolClientExtensions.IsValidReplacementCertificateId(existingCertificateId.CertificateId))
            {
                _logger.LogWarning("Invalid certificate ID for replacement: {CertificateId}",
                    existingCertificateId.CertificateId);
                return false;
            }

            // If we have renewal info, check if we're in the renewal window
            if (renewalInfo?.SuggestedWindow != null)
            {
                var now = DateTime.UtcNow;
                var inWindow = now >= renewalInfo.SuggestedWindow.Start && now <= renewalInfo.SuggestedWindow.End;

                _logger.LogDebug("ARI replacement decision: in renewal window = {InWindow}", inWindow);
                return inWindow;
            }

            // Default to including replacement if we have a valid certificate ID and ARI support
            _logger.LogDebug("No ARI renewal window data - defaulting to include replacement");
            return true;
        }

        /// <summary>
        /// Logs order creation details for monitoring and debugging
        /// </summary>
        /// <param name="order">The created order</param>
        /// <param name="identifiers">The requested identifiers</param>
        /// <param name="usedReplacement">Whether replacement was used</param>
        public void LogOrderCreation(OrderDetails order, IReadOnlyList<string> identifiers, bool usedReplacement)
        {
            if (order == null || identifiers == null)
            {
                return;
            }

            _logger.LogInformation("ACME Order Created - URL: {OrderUrl}, Status: {Status}, Domains: {DomainCount}, ARI Replacement: {UsedReplacement}",
                order.OrderUrl, order.Payload?.Status, identifiers.Count, usedReplacement);

            if (order.Payload?.Authorizations != null && order.Payload.Authorizations.Any())
            {
                _logger.LogDebug("Order authorizations required: {AuthorizationCount}",
                    order.Payload.Authorizations.Length);
            }
        }
    }
}
