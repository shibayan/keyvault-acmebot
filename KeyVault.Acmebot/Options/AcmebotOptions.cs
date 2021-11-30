using System.ComponentModel.DataAnnotations;

namespace KeyVault.Acmebot.Options
{
    public class AcmebotOptions
    {
        [Required]
        public string Endpoint { get; set; } = "https://acme-v02.api.letsencrypt.org/";

        [Required]
        public string Contacts { get; set; }

        // Backward compatibility, Remove in the future
        public string SubscriptionId { get; set; }

        [Required]
        public string VaultBaseUrl { get; set; }

        [Url]
        public string Webhook { get; set; }

        [Required]
        public string Environment { get; set; } = "AzureCloud";

        public string PreferredChain { get; set; }

        public bool MitigateChainOrder { get; set; } = false;

        public ExternalAccountBindingOptions ExternalAccountBinding { get; set; }

        // Properties should be in alphabetical order
        public AzureDnsOptions AzureDns { get; set; }

        public CloudflareOptions Cloudflare { get; set; }

        public CustomDnsOptions CustomDns { get; set; }

        public DnsMadeEasyOptions DnsMadeEasy { get; set; }

        public GandiOptions Gandi { get; set; }

        public GoDaddyOptions GoDaddy { get; set; }

        // Backward compatibility, Remove in the future
        public GoogleDnsOptions Google { get; set; }

        public GoogleDnsOptions GoogleDns { get; set; }

        public GratisDnsOptions GratisDns { get; set; }

        public Route53Options Route53 { get; set; }

        public TransIpOptions TransIp { get; set; }
    }
}
