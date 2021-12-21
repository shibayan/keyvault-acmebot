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

        [JsonProperty("createdOn")]
        public DateTimeOffset CreatedOn { get; set; }

        [JsonProperty("expiresOn")]
        public DateTimeOffset ExpiresOn { get; set; }

        [JsonProperty("x509Thumbprint")]
        public string X509Thumbprint { get; set; }

        [JsonProperty("keyType")]
        public string KeyType { get; set; }

        [JsonProperty("keySize")]
        public int? KeySize { get; set; }

        [JsonProperty("keyCurveName")]
        public string KeyCurveName { get; set; }

        [JsonProperty("reuseKey")]
        public bool? ReuseKey { get; set; }

        [JsonProperty("isManaged")]
        public bool IsManaged { get; set; }

        [JsonProperty("isExpired")]
        public bool IsExpired { get; set; }
    }
}
