using System.ComponentModel.DataAnnotations;

namespace KeyVault.Acmebot.Options
{
    public class AcmebotOptions
    {
        [Required]
        public string Endpoint { get; set; } = "https://acme-v02.api.letsencrypt.org/";

        [Required]
        public string Contacts { get; set; }

        // Backward compatibility
        public string SubscriptionId { get; set; }

        [Required]
        public string VaultBaseUrl { get; set; }

        [Url]
        public string Webhook { get; set; }

        public AzureDnsOptions AzureDns { get; set; }

        public CloudflareOptions Cloudflare { get; set; }

        public GratisDnsOptions GratisDns { get; set; }
    }
}
