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
    public class GandiProvider : IDnsProvider
    {
        public GandiProvider(GandiOptions options)
        {
            _client = new GandiClient(options.ApiKey);
        }

        private readonly GandiClient _client;

        public int PropagationSeconds => 300;

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = await _client.ListZonesAsync();

            // Do NOT include the PrimaryNameServer element from the DnsZone list for now,
            // the return value from Gandi when returning zones is not the expected value when doing the intersect at the Dns01Precondition method

            return zones.Select(x => new DnsZone { Id = x.Uuid, Name = x.Name }).ToArray();
        }

        public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            return _client.AddRecordAsync(zone.Name, relativeRecordName, values);
        }

        public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            return _client.DeleteRecordAsync(zone.Name, relativeRecordName);
        }

        private class GandiClient
        {
            public GandiClient(string apiKey)
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

            public async Task<IReadOnlyList<Zone>> ListZonesAsync()
            {
                var response = await _httpClient.GetAsync("zones");

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsAsync<Zone[]>();
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
                    rrset_ttl = 300 //300 is the minimal value
                });

                response.EnsureSuccessStatusCode();
            }
        }
        public class Zone
        {
            [JsonProperty("uuid")]
            public string Uuid { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("primary_ns")]
            public string PrimaryNameServer { get; set; }

            [JsonProperty("email")]
            public string Email { get; set; }

            [JsonProperty("serial")]
            public int Serial { get; set; }

            [JsonProperty("user_uuid")]
            public string UserUuid { get; set; }

            [JsonProperty("refresh")]
            public int Refresh { get; set; }

            [JsonProperty("minimum")]
            public int Minimum { get; set; }

            [JsonProperty("expire")]
            public int Expire { get; set; }

            [JsonProperty("retry")]
            public int Retry { get; set; }
        }
    }
}
