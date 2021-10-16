using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;

namespace KeyVault.Acmebot.Providers
{
    public class GandiDnsProvider : IDnsProvider
    {
        public GandiDnsProvider(GandiLiveDnsOptions options)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://dns.api.gandi.net/api/v5/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", options.ApiKey);

            PropagationSeconds = 10;
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

            var response = await _httpClient.PostAsync($"zones/{zone.Id}/records", new
            {
                rrset_name = recordName,
                rrset_type = "A",
                rrset_ttl = 60,
                rrset_values = values.ToArray()
            });

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
