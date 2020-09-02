using Azure.Security.KeyVault.Certificates;

using KeyVault.Acmebot.Models;

namespace KeyVault.Acmebot.Internal
{
    internal static class CertificateExtensions
    {
        public static bool TagsFilter(this CertificateProperties properties, string issuer, string endpoint)
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
            return new CertificateItem
            {
                Id = certificate.Id,
                Name = certificate.Name,
                DnsNames = certificate.Policy.SubjectAlternativeNames.DnsNames,
                ExpiresOn = certificate.Properties.ExpiresOn.Value
            };
        }
    }
}
