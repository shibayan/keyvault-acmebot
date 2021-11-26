using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Providers
{
    public class GoDaddyProvider : IDnsProvider
    {
        public GoDaddyProvider(GoDaddyOptions options)
        {
            _client = new GoDaddyClient(options.ApiKey, options.ApiSecret);
        }

        private readonly GoDaddyClient _client;

        public int PropagationSeconds => 600;

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = await _client.ListZonesAsync();

            return zones.Select(x => new DnsZone { Id = x.DomainId, Name = x.Domain, NameServers = x.NameServers }).ToArray();
        }

        public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            var entries = new List<DnsEntry>();

            foreach (var value in values)
            {
                entries.Add(new DnsEntry
                {
                    Name = relativeRecordName,
                    Type = "TXT",
                    TTL = 600,
                    Data = value
                });
            }

            return _client.AddRecordAsync(zone.Name, entries);
        }

        public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            return _client.DeleteRecordAsync(zone.Name, "TXT", relativeRecordName);
        }

        private class GoDaddyClient
        {
            public GoDaddyClient(string apiKey, string apiSecret)
            {
                if (apiKey is null)
                {
                    throw new ArgumentNullException(nameof(apiKey));
                }

                if (apiSecret is null)
                {
                    throw new ArgumentNullException(nameof(apiSecret));
                }

                _httpClient = new HttpClient
                {
                    BaseAddress = new Uri("https://api.godaddy.com")
                };

                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("sso-key", $"{apiKey}:{apiSecret}");
            }

            private readonly HttpClient _httpClient;

            public async Task<IReadOnlyList<ZoneDomain>> ListZonesAsync()
            {
                var response = await _httpClient.GetAsync("v1/domains?statuses=ACTIVE&includes=nameServers");

                response.EnsureSuccessStatusCode();

                var domains = await response.Content.ReadAsAsync<ZoneDomain[]>();

                return domains;
            }

            public async Task DeleteRecordAsync(string domain, string type, string name)
            {
                var response = await _httpClient.DeleteAsync($"v1/domains/{domain}/records/{type}/{name}");

                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    response.EnsureSuccessStatusCode();
                }
            }

            public async Task AddRecordAsync(string domain, IReadOnlyList<DnsEntry> entries)
            {
                var response = await _httpClient.PatchAsync($"v1/domains/{domain}/records", entries);

                response.EnsureSuccessStatusCode();
            }
        }

        public class ZoneDomain
        {
            [JsonProperty("domain")]
            public string Domain { get; set; }

            [JsonProperty("domainId")]
            public string DomainId { get; set; }

            [JsonProperty("nameServers")]
            public string[] NameServers { get; set; }
        }

        public class DnsEntry
        {
            [JsonProperty("data")]
            public string Data { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("ttl")]
            public int TTL { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }
        }
    }
}
