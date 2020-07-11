using System.ComponentModel.DataAnnotations;

namespace KeyVault.Acmebot
{
    public class AcmebotOptions
    {
        [Required]
        public string Endpoint { get; set; } = "https://acme-v02.api.letsencrypt.org/";

        [Required]
        public string Contacts { get; set; }

        public string SubscriptionId
        {
            set => DnsProvider.SubscriptionId = value;
        }

        [Required]
        public string VaultBaseUrl { get; set; }

        [Url]
        public string Webhook { get; set; }

        public AzureDnsOptions DnsProvider { get; set; } = new AzureDnsOptions();
    }

    public class AzureDnsOptions
    {
        [Required]
        public string SubscriptionId { get; set; }
    }
}
