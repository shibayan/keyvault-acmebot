namespace KeyVault.Acmebot
{
    public class LetsEncryptOptions
    {
        public string Contacts { get; set; }

        public string SubscriptionId { get; set; }

        public string VaultBaseUrl { get; set; }

        public string Webhook { get; set; }
    }
}
