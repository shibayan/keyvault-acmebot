using System;
using System.Collections.Generic;

using Azure.Security.KeyVault.Certificates;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Models
{
    public class GetCertificateResponse
    {
        public GetCertificateResponse(KeyVaultCertificateWithPolicy certificateBundle)
        {
            Name = certificateBundle.Name;
            DnsNames = certificateBundle.Policy.SubjectAlternativeNames.DnsNames;
            Expire = certificateBundle.Properties.ExpiresOn;
        }

        [JsonProperty("name")]
        public string Name { get; }

        [JsonProperty("dnsNames")]
        public IList<string> DnsNames { get; }

        [JsonProperty("expire")]
        public DateTimeOffset? Expire { get; }
    }
}
