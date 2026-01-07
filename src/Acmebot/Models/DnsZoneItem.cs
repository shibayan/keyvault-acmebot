using Newtonsoft.Json;

namespace Acmebot.Models;

public class DnsZoneItem
{
    [JsonProperty("name")]
    public required string Name { get; set; }
}

public class DnsZoneGroup
{
    [JsonProperty("dnsProviderName")]
    public required string DnsProviderName { get; set; }

    [JsonProperty("dnsZones")]
    public required IReadOnlyList<DnsZoneItem> DnsZones { get; set; }
}
