using Newtonsoft.Json;

namespace CustomDns.Models;

public class UpsertRecordRequest
{
    [JsonProperty("ttl")]
    public int TTL { get; set; }

    [JsonProperty("values")]
    public string[] Values { get; set; }
}
