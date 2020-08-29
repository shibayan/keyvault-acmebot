using Azure.Security.KeyVault.Certificates;

namespace KeyVault.Acmebot.Internal
{
    internal static class CertificatePropertiesExtensions
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
    }
}
