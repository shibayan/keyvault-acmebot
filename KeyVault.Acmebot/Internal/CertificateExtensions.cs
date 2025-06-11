using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

using Azure.Security.KeyVault.Certificates;

using KeyVault.Acmebot.Models;
using KeyVault.Acmebot.Internal;

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
        var certificateId = x509Certificate.GenerateCertificateId();

        return certificateId;
    } 
    
    private const string IssuerKey = "Issuer";
    private const string EndpointKey = "Endpoint";
    private const string DnsProviderKey = "DnsProvider";
    private const string DnsAliasKey = "DnsAlias";

    private const string IssuerValue = "Acmebot";

    private static bool TryGetIssuer(this IDictionary<string, string> tags, out string issuer) => tags.TryGetValue(IssuerKey, out issuer);

    private static bool TryGetEndpoint(this IDictionary<string, string> tags, out string endpoint) => tags.TryGetValue(EndpointKey, out endpoint);

    private static bool TryGetDnsProvider(this IDictionary<string, string> tags, out string dnsProviderName) => tags.TryGetValue(DnsProviderKey, out dnsProviderName);

    private static bool TryGetDnsAlias(this IDictionary<string, string> tags, out string dnsAlias) => tags.TryGetValue(DnsAliasKey, out dnsAlias);

    private static string NormalizeEndpoint(string endpoint) => Uri.TryCreate(endpoint, UriKind.Absolute, out var legacyEndpoint) ? legacyEndpoint.Host : endpoint;
}
