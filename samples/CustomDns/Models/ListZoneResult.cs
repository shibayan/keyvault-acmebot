using System.Collections.Generic;

using Newtonsoft.Json;

namespace CustomDns.Models;

public class ListZoneResult
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("nameServers")]
    public IReadOnlyList<string> NameServers { get; set; }
}
