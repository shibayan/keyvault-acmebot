using System.ComponentModel.DataAnnotations;

namespace KeyVault.Acmebot
{
    public class AcmebotOptions
    {
        [Required]
        public string Endpoint { get; set; } = "https://acme-v02.api.letsencrypt.org/";

        [Required]
        public string Contacts { get; set; }

        [Required]
        public string SubscriptionId { get; set; }

        [Required]
        public string VaultBaseUrl { get; set; }

        [Url]
        public string Webhook { get; set; }
    }
}
