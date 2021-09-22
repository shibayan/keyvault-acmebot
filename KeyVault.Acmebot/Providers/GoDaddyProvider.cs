﻿using System;
using System.Collections.Generic;
using System.Linq;
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

            return zones.Select(x => new DnsZone { Id = x.Domain, Name = x.Domain }).ToArray();
        }

        public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
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

            await _client.AddRecordAsync(zone.Id, entries);
        }

        public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            var records = await _client.ListRecordsAsync(zone.Id);

            var recordsToDelete = records.Where(r => r.Name == relativeRecordName && r.Type == "TXT");

            foreach (var record in recordsToDelete)
            {
                await _client.DeleteRecordAsync(zone.Id, record);
            }
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
                var response = await _httpClient.GetAsync("v1/domains?statuses=ACTIVE");

                response.EnsureSuccessStatusCode();

                var domains = await response.Content.ReadAsAsync<ZoneDomain[]>();

                return domains;
            }

            public async Task<IReadOnlyList<DnsEntry>> ListRecordsAsync(string zoneId)
            {
                var response = await _httpClient.GetAsync($"v1/domains/{zoneId}/records");

                response.EnsureSuccessStatusCode();

                var entries = await response.Content.ReadAsAsync<DnsEntry[]>();

                return entries;
            }

            public async Task DeleteRecordAsync(string zoneId, DnsEntry entry)
            {
                var response = await _httpClient.DeleteAsync($"v1/domains/{zoneId}/records/{entry.Type}/{entry.Name}");

                response.EnsureSuccessStatusCode();
            }

            public async Task AddRecordAsync(string zoneId, IReadOnlyList<DnsEntry> entries)
            {
                var response = await _httpClient.PatchAsync($"v1/domains/{zoneId}/records", entries);

                response.EnsureSuccessStatusCode();
            }
        }

        public class ZoneDomain
        {
            [JsonProperty("domain")]
            public string Domain { get; set; }

            [JsonProperty("domainId")]
            public string DomainId { get; set; }
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

            [JsonProperty("port")]
            public int? Port { get; set; }

            [JsonProperty("priority")]
            public int? Priority { get; set; }

            [JsonProperty("protocol")]
            public string Protocol { get; set; }

            [JsonProperty("service")]
            public string Service { get; set; }

            [JsonProperty("weight")]
            public int? Weight { get; set; }
        }
    }
}
