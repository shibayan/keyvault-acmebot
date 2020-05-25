using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Models
{
    public class GetCertificateResponse
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("domains")]
        public IList<string> Domains { get; set; }

        [JsonProperty("expire")]
        public DateTime? Expire { get; set; }
    }
}
