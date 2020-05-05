namespace KeyVault.Acmebot
{
    public class AcmebotOptions
    {
        public string Endpoint { get; set; } = "https://acme-v02.api.letsencrypt.org/";

        public string Contacts { get; set; }

        public string SubscriptionId { get; set; }

        public string VaultBaseUrl { get; set; }

        public string Webhook { get; set; }
    }
}
