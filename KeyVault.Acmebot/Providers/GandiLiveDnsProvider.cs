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

public class GandiLiveDnsProvider : IDnsProvider
{
    public GandiLiveDnsProvider(GandiLiveDnsOptions options)
    {
        _client = new GandiLiveDnsClient(options.ApiKey);
    }

    private readonly GandiLiveDnsClient _client;

    public string Name => "Gandi LiveDNS";

    public int PropagationSeconds => 300;

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var zones = await _client.ListZonesAsync();

        return zones.Select(x => new DnsZone(this) { Id = x.Fqdn, Name = x.FqdnUnicode }).ToArray();
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        return _client.AddRecordAsync(zone.Name, relativeRecordName, values);
    }

    public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        return _client.DeleteRecordAsync(zone.Name, relativeRecordName);
    }

    private class GandiLiveDnsClient
    {
        public GandiLiveDnsClient(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(apiKey);

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.gandi.net/v5/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
        }

        private readonly HttpClient _httpClient;

        public async Task<IReadOnlyList<Domain>> ListZonesAsync()
        {
            var response = await _httpClient.GetAsync("domain/domains");

            response.EnsureSuccessStatusCode();
            var domains = await response.Content.ReadAsAsync<Domain[]>();

            return domains.Where(x => x.Nameserver.Current == "livedns").ToArray();
        }

        public async Task DeleteRecordAsync(string zoneName, string relativeRecordName)
        {
            var response = await _httpClient.DeleteAsync($"livedns/domains/{zoneName}/records/{relativeRecordName}/TXT");

            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task AddRecordAsync(string zoneName, string relativeRecordName, IEnumerable<string> values)
        {
            var response = await _httpClient.PostAsync($"livedns/domains/{zoneName}/records/{relativeRecordName}/TXT", new
            {
                rrset_values = values.ToArray(),
                rrset_ttl = 300 //300 is the minimal value
            });

            response.EnsureSuccessStatusCode();
        }
    }
    public class Domain
    {
        [JsonProperty("fqdn")]
        public string Fqdn { get; set; }

        [JsonProperty("tld")]
        public string Tld { get; set; }

        [JsonProperty("status")]
        public List<string> Status { get; set; }

        [JsonProperty("dates")]
        public Dates Dates { get; set; }

        [JsonProperty("nameserver")]
        public Nameserver Nameserver { get; set; }

        [JsonProperty("autorenew")]
        public bool Autorenew { get; set; }

        [JsonProperty("domain_owner")]
        public string DomainOwner { get; set; }

        [JsonProperty("orga_owner")]
        public string OrgaOwner { get; set; }

        [JsonProperty("owner")]
        public string Owner { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("href")]
        public string Href { get; set; }

        [JsonProperty("fqdn_unicode")]
        public string FqdnUnicode { get; set; }
    }

    public class Dates
    {
        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("registry_created_at")]
        public DateTime RegistryCreatedAt { get; set; }

        [JsonProperty("registry_ends_at")]
        public DateTime RegistryEndsAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    public class Nameserver
    {
        [JsonProperty("current")]
        public string Current { get; set; }
    }
}
