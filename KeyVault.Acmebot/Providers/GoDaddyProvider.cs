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

namespace KeyVault.Acmebot.Providers;

public class GoDaddyProvider : IDnsProvider
{
    public GoDaddyProvider(GoDaddyOptions options)
    {
        _client = new GoDaddyClient(options.ApiKey, options.ApiSecret);
    }

    private readonly GoDaddyClient _client;

    public string Name => "GoDaddy";

    public int PropagationSeconds => 600;

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var zones = await _client.ListZonesAsync();

        return zones.Select(x => new DnsZone(this) { Id = x.DomainId, Name = x.Domain, NameServers = x.NameServers }).ToArray();
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        var entries = values.Select(x => new DnsEntry { Name = relativeRecordName, Type = "TXT", TTL = 600, Data = x }).ToArray();

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
            ArgumentNullException.ThrowIfNull(apiKey);
            ArgumentNullException.ThrowIfNull(apiSecret);

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.godaddy.com/v1/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("sso-key", $"{apiKey}:{apiSecret}");
        }

        private readonly HttpClient _httpClient;

        public async Task<IReadOnlyList<ZoneDomain>> ListZonesAsync()
        {
            const int limit = 100;

            var marker = "";
            var allActiveDomains = new List<ZoneDomain>();

            while (true)
            {
                var response = await _httpClient.GetAsync($"domains?statuses=ACTIVE&includes=nameServers&limit={limit}{marker}");

                response.EnsureSuccessStatusCode();

                var domains = await response.Content.ReadAsAsync<ZoneDomain[]>();

                if (domains.Length == 0)
                {
                    break;
                }

                allActiveDomains.AddRange(domains);

                marker = $"&marker={domains[^1].Domain}";
            }

            return allActiveDomains;
        }

        public async Task DeleteRecordAsync(string domain, string type, string name)
        {
            var response = await _httpClient.DeleteAsync($"domains/{domain}/records/{type}/{name}");

            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task AddRecordAsync(string domain, IReadOnlyList<DnsEntry> entries)
        {
            var response = await _httpClient.PatchAsync($"domains/{domain}/records", entries);

            response.EnsureSuccessStatusCode();
        }
    }

    private class ZoneDomain
    {
        [JsonProperty("domain")]
        public string Domain { get; set; }

        [JsonProperty("domainId")]
        public string DomainId { get; set; }

        [JsonProperty("nameServers")]
        public string[] NameServers { get; set; }
    }

    private class DnsEntry
    {
        [JsonProperty("data")]
        public string Data { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        // ReSharper disable once InconsistentNaming
        [JsonProperty("ttl")]
        public int TTL { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
