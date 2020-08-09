using Microsoft.Azure.KeyVault.Models;

namespace KeyVault.Acmebot.Internal
{
    internal static class CertificateItemExtensions
    {
        public static bool TagsFilter(this CertificateItem certificate, string issuer, string endpoint)
        {
            var tags = certificate.Tags;

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
    }
}
