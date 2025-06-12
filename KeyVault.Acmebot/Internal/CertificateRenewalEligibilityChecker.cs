using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

using ACMESharp.Protocol;

using Azure.Security.KeyVault.Certificates;

using KeyVault.Acmebot.Models;
using KeyVault.Acmebot.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KeyVault.Acmebot.Internal;

/// <summary>
/// This service evaluates certificates to determine if they should be renewed based on ARI recommendations or expiry date.
/// </summary>
public class CertificateRenewalEligibilityChecker
{
    private readonly ILogger<CertificateRenewalEligibilityChecker> _logger;
    private readonly AcmebotOptions _options;
    private readonly HttpClient _httpClient;

    public CertificateRenewalEligibilityChecker(
        ILogger<CertificateRenewalEligibilityChecker> logger,
        IOptions<AcmebotOptions> options,
        HttpClient httpClient
        )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Evaluates a certificate for renewal using ARI or expiry-based logic 
    /// </summary>
    /// <param name="certificate">Certificate properties</param>
    /// <param name="currentDateTime">Current date/time for expiry calculations</param>
    /// <param name="acmeProtocolClient">ACME client for ARI operations</param>
    /// <param name="certificateClient">Certificate client for retrieving full certificate data</param>
    /// <returns>CertificateItem if renewal is needed, null otherwise</returns>
    public async Task<bool> IsCertificateDueForRenewalAsync(
        KeyVaultCertificateWithPolicy certificate,
        DateTime currentDateTime,
        string ARIEndpoint)

    {
        try
        {

            var shouldARIRenew = await IsEligibleforARIRenewal(ARIEndpoint, certificate);
            var shouldExpiryRenew = IsEligibleForExpiryBasedRenewal(certificate.Properties, currentDateTime);

            if (shouldARIRenew || shouldExpiryRenew)
            {
                return true;
            }

            _logger.LogDebug("Certificate {Name} does not require renewal based on ARI or expiry checks", certificate.Name);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating certificate {Name} for ARI renewal. Falling back to expiry-based renewal", certificate.Name);
            return IsEligibleForExpiryBasedRenewal(certificate.Properties, currentDateTime);
        }
    }

    /// <summary>
    /// Determines if a certificate should be renewed based on ARI recommendations
    /// </summary>
    private async Task<bool> IsEligibleforARIRenewal(
        string ARIEndpoint,
        KeyVaultCertificateWithPolicy certificate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check ARI configuration
            if (!_options.AriEnabled || string.IsNullOrEmpty(ARIEndpoint))
            {
                _logger.LogDebug("ARI is not enabled or RenewalInfo is not configured.");
                return false;
            }

            var certificateId = certificate.ExtractARICertificateId();
            var ariUrl = $"{ARIEndpoint.TrimEnd('/')}/{certificateId}";

            _logger.LogDebug("Requesting renewal info from ARI endpoint: {AriUrl}", ariUrl);

            using var response = await _httpClient.GetAsync(ariUrl, cancellationToken);

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var renewalInfo = await response.Content.ReadFromJsonAsync<RenewalInfoResponse>(cancellationToken: cancellationToken);

                    if (renewalInfo?.SuggestedWindow != null)
                    {
                        _logger.LogInformation("Successfully retrieved ARI data. Renewal window: {Start} to {End}. Reason: {ExplanationUrl}",
                            renewalInfo.SuggestedWindow.Start, renewalInfo.SuggestedWindow.End, renewalInfo.ExplanationUrl);

                        // Check if the current date is >= 2/3 of the suggested renewal window duration                        

                        var windowDuration = renewalInfo.SuggestedWindow.End - renewalInfo.SuggestedWindow.Start;
                        if (windowDuration.TotalDays <= 0)
                        {
                            _logger.LogWarning("Invalid ARI suggested window duration: {Duration} days", windowDuration.TotalDays);
                            return false;
                        }
                        var twoThirdsOfWindow = renewalInfo.SuggestedWindow.Start + (windowDuration * 2 / 3);

                        _logger.LogDebug("Certificate : {Name}, Current time: {CurrentTime}, 2/3 window: {TwoThirdsOfWindow}", certificate.Name, DateTime.UtcNow, twoThirdsOfWindow);

                        return DateTime.UtcNow >= twoThirdsOfWindow;

                    }

                    _logger.LogWarning("ARI response missing required SuggestedWindow data");
                    return false;

                default:
                    _logger.LogError("Unexpected HTTP status code from ARI endpoint: {StatusCode}", response.StatusCode);
                    return false;
            }

        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            _logger.LogError(ex, "Error evaluating ARI renewal for certificate {Name}", certificate.Name);
            throw;
        }
    }

    /// <summary>
    /// Evaluates certificate renewal based on expiry date 
    /// </summary>
    /// <param name="certificate">Certificate properties</param>
    /// <param name="currentDateTime">Current date/time</param>
    /// <param name="fullCertificate">Full certificate with policy</param>
    /// <returns>CertificateItem if renewal needed, null otherwise</returns>
    private bool IsEligibleForExpiryBasedRenewal(
        CertificateProperties certificate,
        DateTime currentDateTime
        )
    {
        var shouldRenew = (certificate.ExpiresOn.Value - currentDateTime).TotalDays <= _options.RenewBeforeExpiry;
        return shouldRenew;

    }


}
