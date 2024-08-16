using System.Collections.Generic;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Models;

public class DnsZoneItem
{
    [JsonProperty("name")]
    public string Name { get; set; }
}

public class DnsZoneGroup
{
    [JsonProperty("dnsProviderName")]
    public string DnsProviderName { get; set; }

    [JsonProperty("dnsZones")]
    public IReadOnlyList<DnsZoneItem> DnsZones { get; set; }
}
