using System;
using System.Collections.Generic;

using Microsoft.Azure.KeyVault.Models;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Models
{
    public class GetCertificateResponse
    {
        public GetCertificateResponse(CertificateBundle certificateBundle)
        {
            Name = certificateBundle.CertificateIdentifier.Name;
            Domains = certificateBundle.Policy.X509CertificateProperties.SubjectAlternativeNames.DnsNames;
            Expire = certificateBundle.Attributes.Expires;
        }

        [JsonProperty("name")]
        public string Name { get; }

        [JsonProperty("domains")]
        public IList<string> Domains { get; }

        [JsonProperty("expire")]
        public DateTime? Expire { get; }
    }
}
