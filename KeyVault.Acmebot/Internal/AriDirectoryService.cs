using System;
using ACMESharp.Protocol;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Internal
{
    /// <summary>
    /// Service for managing ACME Renewal Information (ARI) directory operations
    /// </summary>
    public class AriDirectoryService
    {
        private readonly ILogger<AriDirectoryService> _logger;

        public AriDirectoryService(ILogger<AriDirectoryService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Checks if the ACME client supports ARI
        /// </summary>
        /// <param name="acmeProtocolClient">The ACME protocol client</param>
        /// <returns>True if ARI is supported</returns>
        public bool IsAriSupported(AcmeProtocolClient acmeProtocolClient)
        {
            if (acmeProtocolClient == null)
                return false;

            try
            {
                var hasSupport = acmeProtocolClient.HasRenewalInfoSupport();
                
                if (hasSupport)
                {
                    var renewalInfoUrl = acmeProtocolClient.GetRenewalInfoUrl();
                    _logger.LogInformation("ACME server supports ARI with endpoint: {RenewalInfoUrl}", renewalInfoUrl);
                }
                else
                {
                    _logger.LogInformation("ACME server does not support ARI");
                }

                return hasSupport;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking ARI support");
                return false;
            }
        }

        /// <summary>
        /// Gets the renewal info URL for a specific certificate
        /// </summary>
        /// <param name="acmeProtocolClient">The ACME protocol client</param>
        /// <param name="certificateId">The certificate ID</param>
        /// <returns>The renewal info URL or null if not supported</returns>
        public string GetRenewalInfoEndpoint(AcmeProtocolClient acmeProtocolClient, string certificateId)
        {
            if (acmeProtocolClient == null || string.IsNullOrEmpty(certificateId))
                return null;

            try
            {
                if (!IsAriSupported(acmeProtocolClient))
                    return null;

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

        /// <summary>
        /// Validates that an ARI URL is properly formed
        /// </summary>
        /// <param name="ariUrl">The ARI URL to validate</param>
        /// <returns>True if the URL is valid</returns>
        public bool IsValidAriUrl(string ariUrl)
        {
            if (string.IsNullOrEmpty(ariUrl))
                return false;

            try
            {
                var uri = new Uri(ariUrl);
                return uri.Scheme == "https" && !string.IsNullOrEmpty(uri.Host);
            }
            catch
            {
                return false;
            }
        }
    }
}
