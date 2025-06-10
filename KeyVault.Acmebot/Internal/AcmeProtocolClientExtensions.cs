using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;

namespace KeyVault.Acmebot.Internal;

internal static class AcmeProtocolClientExtensions
{
    public static async Task<X509Certificate2Collection> GetOrderCertificateAsync(this AcmeProtocolClient acmeProtocolClient, OrderDetails order,
                                                                                  string preferredChain, CancellationToken cancel = default)
    {
        IEnumerable<string> linkHeaders;
        X509Certificate2Collection defaultX509Certificates;

        using (var resp = await acmeProtocolClient.GetAsync(order.Payload.Certificate, cancel))
        {
            defaultX509Certificates = await resp.Content.ReadAsCertificatesAsync();

            // 証明書チェーンが未指定の場合は即返す
            if (string.IsNullOrEmpty(preferredChain))
            {
                return defaultX509Certificates;
            }

            // Link ヘッダーが存在しない場合は即返す
            if (!resp.Headers.TryGetValues("Link", out linkHeaders))
            {
                return defaultX509Certificates;
            }
        }

        foreach (var linkHeader in linkHeaders)
        {
            // Link ヘッダーから alternate で指定されている URL を拾ってくる
            var rel = Regex.Match(linkHeader, "(?<=rel=\").+?(?=\")", RegexOptions.IgnoreCase);

            if (!string.Equals(rel.Value, "alternate", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var url = Regex.Match(linkHeader, "(?<=<).+?(?=>)", RegexOptions.IgnoreCase);

            // 代替の証明書をダウンロードする
            using var resp = await acmeProtocolClient.GetAsync(url.Value, cancel);

            var x509Certificates = await resp.Content.ReadAsCertificatesAsync();

            // ルート CA の名前が指定された証明書チェーンに一致する場合は返す
            if (x509Certificates[^1].GetNameInfo(X509NameType.DnsName, true) == preferredChain)
            {
                return x509Certificates;
            }
        }

        // マッチする証明書チェーンが存在しない場合はデフォルトを返す
        return defaultX509Certificates;
    }

    /// <summary>
    /// Checks if the ACME server supports Renewal Information (ARI)
    /// </summary>
    /// <param name="acmeProtocolClient">The ACME protocol client</param>
    /// <returns>True if ARI is supported, false otherwise</returns>
    public static bool HasRenewalInfoSupport(this AcmeProtocolClient acmeProtocolClient)
    {
        return !string.IsNullOrEmpty(GetRenewalInfoUrl(acmeProtocolClient));
    }

    /// <summary>
    /// Gets the renewal info URL from the ACME directory
    /// </summary>
    /// <param name="acmeProtocolClient">The ACME protocol client</param>
    /// <returns>The renewal info URL if available, null otherwise</returns>
    public static string GetRenewalInfoUrl(this AcmeProtocolClient acmeProtocolClient)
    {
        // Use the new RenewalInfo property directly from ServiceDirectory
        return acmeProtocolClient?.Directory?.RenewalInfo;
    }

    /// <summary>
    /// Constructs the full renewal info URL for a certificate ID
    /// </summary>
    /// <param name="acmeProtocolClient">The ACME protocol client</param>
    /// <param name="certificateId">The base64url-encoded certificate ID</param>
    /// <returns>The complete URL for the renewal info request</returns>
    public static string GetRenewalInfoUrlForCertificate(this AcmeProtocolClient acmeProtocolClient, string certificateId)
    {
        var baseUrl = GetRenewalInfoUrl(acmeProtocolClient);
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(certificateId))
        {
            return null;
        }

        // Ensure proper URL construction
        var trimmedBaseUrl = baseUrl.TrimEnd('/');
        return $"{trimmedBaseUrl}/{certificateId}";
    }

    /// <summary>
    /// Creates an ACME order with optional certificate replacement support for ARI
    /// </summary>
    /// <param name="acmeProtocolClient">The ACME protocol client</param>
    /// <param name="identifiers">List of domain identifiers for the certificate</param>
    /// <param name="replacesCertificateId">Optional certificate ID that this order replaces (for ARI)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order details</returns>
    public static async Task<OrderDetails> CreateOrderWithReplacementAsync(this AcmeProtocolClient acmeProtocolClient,
        IReadOnlyList<string> identifiers, string replacesCertificateId = null, CancellationToken cancellationToken = default)
    {
        if (identifiers == null || !identifiers.Any())
        {
            throw new ArgumentException("At least one identifier is required", nameof(identifiers));
        }

        try
        {
            // Prepare the order request with standard identifiers
            var orderIdentifiers = identifiers.Select(id => new Identifier 
            { 
                Type = "dns",
                Value = id
            });

            var orderDetails = await acmeProtocolClient.CreateOrderAsync(orderIdentifiers,
                replacesCertificateId: replacesCertificateId,  null, null, cancellationToken);       

            return orderDetails;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Handle cancellation properly even though we can't pass the token to PostAsync
            throw;
        }
        catch (Exception ex)
        {
            // Log error if possible, but don't break if logging fails
            System.Diagnostics.Debug.WriteLine($"Failed to create ACME order: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Validates that a certificate ID is suitable for use in the 'replaces' field
    /// </summary>
    /// <param name="certificateId">The certificate ID to validate</param>
    /// <returns>True if the certificate ID is valid for ARI replacement</returns>
    public static bool IsValidReplacementCertificateId(string certificateId)
    {
        if (string.IsNullOrEmpty(certificateId))
        {
            return false;
        }

        try
        {
            // Basic validation - should be base64url encoded
            // Check length and characters
            if (certificateId.Length < 10 || certificateId.Length > 200)
            {
                return false;                
            }

            // Should only contain base64url characters
            return certificateId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        }
        catch
        {
            return false;
        }
    }
}
