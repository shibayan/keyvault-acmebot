using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

using Azure.Security.KeyVault.Certificates;

using KeyVault.Acmebot.Models;

namespace KeyVault.Acmebot.Internal;

internal static class CertificateExtensions
{
    public static bool IsIssuedByAcmebot(this CertificateProperties properties)
    {
        return properties.Tags.TryGetIssuer(out var tagIssuer) && tagIssuer == IssuerValue;
    }

    public static bool IsSameEndpoint(this CertificateProperties properties, Uri endpoint)
    {
        return properties.Tags.TryGetEndpoint(out var tagEndpoint) && NormalizeEndpoint(tagEndpoint) == endpoint.Host;
    }

    public static CertificateItem ToCertificateItem(this KeyVaultCertificateWithPolicy certificate)
    {
        var dnsNames = certificate.Policy.SubjectAlternativeNames?.DnsNames.ToArray();

        return new CertificateItem
        {
            Id = certificate.Id,
            Name = certificate.Name,
            DnsNames = dnsNames is { Length: > 0 } ? dnsNames : new[] { certificate.Policy.Subject[3..] },
            DnsProviderName = certificate.Properties.Tags.TryGetDnsProvider(out var dnsProviderName) ? dnsProviderName : "",
            CreatedOn = certificate.Properties.CreatedOn.Value,
            ExpiresOn = certificate.Properties.ExpiresOn.Value,
            X509Thumbprint = Convert.ToHexString(certificate.Properties.X509Thumbprint),
            KeyType = certificate.Policy.KeyType?.ToString(),
            KeySize = certificate.Policy.KeySize,
            KeyCurveName = certificate.Policy.KeyCurveName?.ToString(),
            ReuseKey = certificate.Policy.ReuseKey,
            IsExpired = DateTimeOffset.UtcNow > certificate.Properties.ExpiresOn.Value,
            AcmeEndpoint = certificate.Properties.Tags.TryGetEndpoint(out var acmeEndpoint) ? NormalizeEndpoint(acmeEndpoint) : "",
            DnsAlias = certificate.Properties.Tags.TryGetDnsAlias(out var dnsAlias) ? dnsAlias : "",
        };
    }

    public static CertificatePolicyItem ToCertificatePolicyItem(this KeyVaultCertificateWithPolicy certificate)
    {
        var dnsNames = certificate.Policy.SubjectAlternativeNames.DnsNames.ToArray();

        return new CertificatePolicyItem
        {
            CertificateName = certificate.Name,
            DnsNames = dnsNames.Length > 0 ? dnsNames : new[] { certificate.Policy.Subject[3..] },
            DnsProviderName = certificate.Properties.Tags.TryGetDnsProvider(out var dnsProviderName) ? dnsProviderName : "",
            KeyType = certificate.Policy.KeyType?.ToString(),
            KeySize = certificate.Policy.KeySize,
            KeyCurveName = certificate.Policy.KeyCurveName?.ToString(),
            ReuseKey = certificate.Policy.ReuseKey,
            DnsAlias = certificate.Properties.Tags.TryGetDnsAlias(out var dnsAlias) ? dnsAlias : ""
        };
    }

    public static IDictionary<string, string> ToCertificateMetadata(this CertificatePolicyItem certificatePolicyItem, Uri endpoint)
    {
        var metadata = new Dictionary<string, string>
        {
            { IssuerKey, IssuerValue },
            { EndpointKey, endpoint.Host },
            { DnsProviderKey, certificatePolicyItem.DnsProviderName }
        };

        if (!string.IsNullOrEmpty(certificatePolicyItem.DnsAlias))
        {
            metadata.Add(DnsAliasKey, certificatePolicyItem.DnsAlias);
        }

        return metadata;
    }


    /// <summary>
    /// Extracts certificate identifier components from an X.509 certificate
    /// </summary>
    /// <param name="certificate">The X.509 certificate to process</param>
    /// <returns>Certificate identifier with AKI, serial number, and encoded certificate ID</returns>
    /// <exception cref="ArgumentNullException">Thrown when certificate is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when required certificate components cannot be extracted</exception>
    public static string ExtractARICertificateId(this KeyVaultCertificateWithPolicy certificate)
    {
        if (certificate == null)
        {
            throw new ArgumentNullException(nameof(certificate));
        }
         var x509Certificate = new X509Certificate2(certificate.Cer);

        var authorityKeyIdentifier = ExtractAuthorityKeyIdentifier(x509Certificate);
        var serialNumber = ExtractSerialNumber(x509Certificate);
        var certificateId = GenerateCertificateId(authorityKeyIdentifier, serialNumber);

        return certificateId;
    }

    /// <summary>
    /// Extracts Authority Key Identifier from certificate extensions
    /// </summary>
    private static byte[] ExtractAuthorityKeyIdentifier(X509Certificate2 certificate)
    {
        var akiExtension = certificate.Extensions[AuthorityKeyIdentifierOid];
        if (akiExtension == null)
        {
            throw new InvalidOperationException("Certificate does not contain Authority Key Identifier extension");
        }

        // Parse AKI extension according to RFC 5280
        // AKI is an OCTET STRING containing a SEQUENCE with optional keyIdentifier [0]
        var akiBytes = akiExtension.RawData;

        try
        {
            // Simple parsing assuming keyIdentifier is present as [0] tag
            // Look for tag 0x80 (context-specific, primitive, tag 0)
            for (int i = 0; i < akiBytes.Length - 1; i++)
            {
                if (akiBytes[i] == 0x80) // keyIdentifier tag
                {
                    var length = akiBytes[i + 1];
                    if (i + 2 + length <= akiBytes.Length)
                    {
                        var keyId = new byte[length];
                        Array.Copy(akiBytes, i + 2, keyId, 0, length);
                        return keyId;
                    }
                }
            }

            throw new InvalidOperationException("Cannot parse Authority Key Identifier from certificate extension");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to extract Authority Key Identifier from certificate", ex);
        }
    }

    /// <summary>
    /// Extracts serial number from certificate
    /// </summary>
    private static byte[] ExtractSerialNumber(X509Certificate2 certificate)
    {
        try
        {
            // Get serial number as byte array, ensuring proper encoding
            var serialString = certificate.SerialNumber;

            // Serial number is returned as hex string, convert to bytes
            // Handle both even and odd length strings
            if (serialString.Length % 2 != 0)
            {
                serialString = "0" + serialString;
            }

            var serialBytes = new byte[serialString.Length / 2];
            for (int i = 0; i < serialBytes.Length; i++)
            {
                serialBytes[i] = Convert.ToByte(serialString.Substring(i * 2, 2), 16);
            }

            return serialBytes;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to extract serial number from certificate", ex);
        }
    }

    /// <summary>
    /// Generates certificate ID by combining AKI and serial number per ARI specification
    /// Format: base64url(AKI) + "." + base64url(SerialNumber)
    /// </summary>
    private static string GenerateCertificateId(byte[] authorityKeyIdentifier, byte[] serialNumber)
    {
        try
        {
            // Encode each component separately per RFC draft specification
            var akiEncoded = Base64UrlEncode(authorityKeyIdentifier);
            var serialEncoded = Base64UrlEncode(serialNumber);

            // Concatenate with dot separator as per ARI specification
            return $"{akiEncoded}.{serialEncoded}";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to generate certificate ID", ex);
        }
    }

    /// <summary>
    /// Encodes byte array using base64url encoding per RFC 4648 Section 5
    /// </summary>
    private static string Base64UrlEncode(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return string.Empty;
        }

        // Convert to base64 then make it URL-safe
        var base64 = Convert.ToBase64String(data);

        // Replace URL-unsafe characters and remove padding
        return base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }


    private const string IssuerKey = "Issuer";
    private const string EndpointKey = "Endpoint";
    private const string DnsProviderKey = "DnsProvider";
    private const string DnsAliasKey = "DnsAlias";

    private const string IssuerValue = "Acmebot";
    private const string AuthorityKeyIdentifierOid = "2.5.29.35";

    private static bool TryGetIssuer(this IDictionary<string, string> tags, out string issuer) => tags.TryGetValue(IssuerKey, out issuer);

    private static bool TryGetEndpoint(this IDictionary<string, string> tags, out string endpoint) => tags.TryGetValue(EndpointKey, out endpoint);

    private static bool TryGetDnsProvider(this IDictionary<string, string> tags, out string dnsProviderName) => tags.TryGetValue(DnsProviderKey, out dnsProviderName);

    private static bool TryGetDnsAlias(this IDictionary<string, string> tags, out string dnsAlias) => tags.TryGetValue(DnsAliasKey, out dnsAlias);

    private static string NormalizeEndpoint(string endpoint) => Uri.TryCreate(endpoint, UriKind.Absolute, out var legacyEndpoint) ? legacyEndpoint.Host : endpoint;
}
