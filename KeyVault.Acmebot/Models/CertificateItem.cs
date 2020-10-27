using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Models
{
    public class CertificateItem
    {
        [JsonProperty("id")]
        public Uri Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("dnsNames")]
        public IReadOnlyList<string> DnsNames { get; set; }

        [JsonProperty("expiresOn")]
        public DateTimeOffset ExpiresOn { get; set; }
    }
}
