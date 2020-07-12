using System.Collections.Generic;
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
        public string SubscriptionId
        {
            set => (AzureDns ??= new AzureDnsOptions()).SubscriptionId = value;
        }

        [Required]
        public string VaultBaseUrl { get; set; }

        [Url]
        public string Webhook { get; set; }

        public AzureDnsOptions AzureDns { get; set; }
    }
}
