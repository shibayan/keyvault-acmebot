using System;
using System.Linq;
using System.Text;

using Azure.Security.KeyVault.Certificates;

using KeyVault.Acmebot.Models;

namespace KeyVault.Acmebot.Internal;

internal static class CertificateExtensions
{
    public static bool IsAcmebotManaged(this CertificateProperties properties, string issuer, Uri endpoint)
    {
        var tags = properties.Tags;

        if (tags is null)
        {
            return false;
        }

        if (!tags.TryGetValue("Issuer", out var tagIssuer) || tagIssuer != issuer)
        {
            return false;
        }

        if (!tags.TryGetValue("Endpoint", out var tagEndpoint) || NormalizeEndpoint(tagEndpoint) != endpoint.Host)
        {
            return false;
        }

        return true;
    }

    public static CertificateItem ToCertificateItem(this KeyVaultCertificateWithPolicy certificate)
    {
        var dnsNames = certificate.Policy.SubjectAlternativeNames?.DnsNames.ToArray();

        return new CertificateItem
        {
            Id = certificate.Id,
            Name = certificate.Name,
            DnsNames = dnsNames is { Length: > 0 } ? dnsNames : new[] { certificate.Policy.Subject[3..] },
            CreatedOn = certificate.Properties.CreatedOn.Value,
            ExpiresOn = certificate.Properties.ExpiresOn.Value,
            X509Thumbprint = ToHexString(certificate.Properties.X509Thumbprint),
            KeyType = certificate.Policy.KeyType?.ToString(),
            KeySize = certificate.Policy.KeySize,
            KeyCurveName = certificate.Policy.KeyCurveName?.ToString(),
            ReuseKey = certificate.Policy.ReuseKey,
            IsExpired = DateTimeOffset.UtcNow > certificate.Properties.ExpiresOn.Value,
            AcmeEndpoint = certificate.Properties.Tags?.TryGetValue("Endpoint", out var acmeEndpoint) ?? false ? NormalizeEndpoint(acmeEndpoint) : ""
        };
    }

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
