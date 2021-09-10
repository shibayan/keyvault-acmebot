using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;

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
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(options.ApiKeyHeaderName, options.ApiKey);

            PropagationSeconds = options.PropagationSeconds;
        }

        private readonly HttpClient _httpClient;

        public int PropagationSeconds { get; }

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var response = await _httpClient.GetAsync("zones");

            response.EnsureSuccessStatusCode();

            var zones = await response.Content.ReadAsAsync<DnsZone[]>();

            return zones;
        }

        public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            var recordName = $"{relativeRecordName}.{zone.Name}";

            var response = await _httpClient.PutAsync($"zones/{zone.Id}/records/{recordName}", new { type = "TXT", ttl = 60, values });

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
