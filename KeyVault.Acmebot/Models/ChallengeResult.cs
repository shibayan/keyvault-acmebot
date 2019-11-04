namespace KeyVault.Acmebot.Models
{
    public class ChallengeResult
    {
        public string Url { get; set; }
        public string DnsRecordName { get; set; }
        public string DnsRecordValue { get; set; }
    }
}