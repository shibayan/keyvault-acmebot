using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;

namespace KeyVault.Acmebot.Providers
{
    public class GandiDnsProvider : IDnsProvider
    {
        public GandiDnsProvider(GandDnsOptions options)
        {
            _client = new GandiDnsClient(options.ApiKey);
        }

        private readonly GandiDnsClient _client;

        public int PropagationSeconds => 10;

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = await _client.ListZonesAsync();

            return zones;
        }

        public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            await _client.AddRecordAsync(zone.Name, relativeRecordName, values);
        }

        public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            await _client.DeleteRecordAsync(zone.Name, relativeRecordName);
        }

        private class GandiDnsClient
        {
            public GandiDnsClient(string apiKey)
            {
                if (apiKey is null)
                {
                    throw new ArgumentNullException(nameof(apiKey));
                }

                _httpClient = new HttpClient
                {
                    BaseAddress = new Uri("https://dns.api.gandi.net/api/v5/")
                };

                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", apiKey);
            }

            private readonly HttpClient _httpClient;

            public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
            {
                var response = await _httpClient.GetAsync("zones");

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsAsync<DnsZone[]>();
            }

            public async Task DeleteRecordAsync(string zoneName, string relativeRecordName)
            {
                var response = await _httpClient.DeleteAsync($"domains/{zoneName}/records/{relativeRecordName}/TXT");

                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    response.EnsureSuccessStatusCode();
                }
            }

            public async Task AddRecordAsync(string zoneName, string relativeRecordName, IEnumerable<string> values)
            {
                var response = await _httpClient.PostAsync($"domains/{zoneName}/records/{relativeRecordName}/TXT", new
                {
                    rrset_values = values.ToArray(),
                    rrset_ttl = 60
                });

                response.EnsureSuccessStatusCode();
            }
        }
    }
}
