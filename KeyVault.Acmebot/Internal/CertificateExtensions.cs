using System;
using System.Linq;
using System.Text;

using Azure.Security.KeyVault.Certificates;

using KeyVault.Acmebot.Models;

namespace KeyVault.Acmebot.Internal
{
    internal static class CertificateExtensions
    {
        public static bool IsAcmebotManaged(this CertificateProperties properties, string issuer, string endpoint)
        {
            var tags = properties.Tags;

            if (tags == null)
            {
                return false;
            }

            if (!tags.TryGetValue("Issuer", out var tagIssuer) || tagIssuer != issuer)
            {
                return false;
            }

            if (!tags.TryGetValue("Endpoint", out var tagEndpoint) || tagEndpoint != endpoint)
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
                DnsNames = dnsNames != null && dnsNames.Length > 0 ? dnsNames : new[] { certificate.Policy.Subject[3..] },
                CreatedOn = certificate.Properties.CreatedOn.Value,
                ExpiresOn = certificate.Properties.ExpiresOn.Value,
                X509Thumbprint = ToHexString(certificate.Properties.X509Thumbprint),
                KeyType = certificate.Policy.KeyType?.ToString(),
                KeySize = certificate.Policy.KeySize,
                KeyCurveName = certificate.Policy.KeyCurveName?.ToString(),
                ReuseKey = certificate.Policy.ReuseKey,
                IsExpired = DateTimeOffset.UtcNow > certificate.Properties.ExpiresOn.Value
            };
        }

        private static string ToHexString(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var result = new StringBuilder();

            foreach (var b in bytes)
            {
                result.Append(b.ToString("x2"));
            }

            return result.ToString();
        }
    }
}
