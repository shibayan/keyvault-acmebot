using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            X509Thumbprint = ToHexString(certificate.Properties.X509Thumbprint),
            KeyType = certificate.Policy.KeyType?.ToString(),
            KeySize = certificate.Policy.KeySize,
            KeyCurveName = certificate.Policy.KeyCurveName?.ToString(),
            ReuseKey = certificate.Policy.ReuseKey,
            IsExpired = DateTimeOffset.UtcNow > certificate.Properties.ExpiresOn.Value,
            AcmeEndpoint = certificate.Properties.Tags.TryGetEndpoint(out var acmeEndpoint) ? NormalizeEndpoint(acmeEndpoint) : ""
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
            ReuseKey = certificate.Policy.ReuseKey
        };
    }

    private const string IssuerKey = "Issuer";
    private const string EndpointKey = "Endpoint";
    private const string DnsProviderKey = "DnsProvider";

    private const string IssuerValue = "Acmebot";

    private static bool TryGetIssuer(this IDictionary<string, string> tags, out string issuer) => tags.TryGetValue(IssuerKey, out issuer);

    private static bool TryGetEndpoint(this IDictionary<string, string> tags, out string endpoint) => tags.TryGetValue(EndpointKey, out endpoint);

    private static bool TryGetDnsProvider(this IDictionary<string, string> tags, out string dnsProviderName) => tags.TryGetValue(DnsProviderKey, out dnsProviderName);

    private static string ToHexString(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var result = new StringBuilder();

        foreach (var b in bytes)
        {
            result.Append(b.ToString("x2"));
        }

        return result.ToString();
    }

    private static string NormalizeEndpoint(string endpoint) => Uri.TryCreate(endpoint, UriKind.Absolute, out var legacyEndpoint) ? legacyEndpoint.Host : endpoint;
}
