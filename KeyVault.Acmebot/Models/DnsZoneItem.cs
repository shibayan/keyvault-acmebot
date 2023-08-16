using Newtonsoft.Json;

namespace KeyVault.Acmebot.Models;

public class DnsZoneItem
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("dnsProviderName")]
    public string DnsProviderName { get; set; }
}
