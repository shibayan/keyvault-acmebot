namespace KeyVault.Acmebot.Options
{
    public class CustomDnsOptions
    {
        public int PropagationSeconds { get; set; } = 180;

        public string Endpoint { get; set; }

        public string ApiKey { get; set; }
    }
}
