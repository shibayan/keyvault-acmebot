using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using KeyVault.Acmebot.Options;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Providers
{
    public class CustomDnsProvider : IDnsProvider
    {
        public CustomDnsProvider(CustomDnsOptions options)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.Endpoint)
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            PropagationSeconds = options.PropagationSeconds;
        }

        private readonly HttpClient _httpClient;

        public int PropagationSeconds { get; }

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var content = await _httpClient.GetStringAsync("zones");

            return JsonConvert.DeserializeObject<DnsZone[]>(content);
        }

        public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            var recordName = $"{relativeRecordName}.{zone.Name}";

            var content = new StringContent(JsonConvert.SerializeObject(new { type = "TXT", ttl = 60, values }), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"zones/{zone.Id}/records/{recordName}", content);

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            var recordName = $"{relativeRecordName}.{zone.Name}";

            var response = await _httpClient.DeleteAsync($"zones/{zone.Id}/records/{recordName}");

            response.EnsureSuccessStatusCode();
        }
    }
}
