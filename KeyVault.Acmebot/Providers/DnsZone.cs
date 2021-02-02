using System.Collections.Generic;

namespace KeyVault.Acmebot.Providers
{
    public class DnsZone
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public IReadOnlyList<string> NameServers { get; set; }
    }
}
