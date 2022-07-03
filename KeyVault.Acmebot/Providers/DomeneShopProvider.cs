using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;

namespace KeyVault.Acmebot.Providers;

public class DomeneShopDnsProvider : IDnsProvider
{
    private const string Endpoint = @"https://api.domeneshop.no/v0/";
    private const string AuthorizationHeader = @"Authorization";

    public DomeneShopDnsProvider(DomeneShopDnsOptions options)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(Endpoint)
        };

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(AuthorizationHeader, CreateAuthorizationHeader(options));

        PropagationSeconds = options.PropagationSeconds;
    }

    private string CreateAuthorizationHeader(DomeneShopDnsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKeyUser)) { throw new ArgumentNullException(nameof(options.ApiKeyUser)); }
        if (string.IsNullOrWhiteSpace(options.ApiKeyPassword)) { throw new ArgumentNullException(nameof(options.ApiKeyPassword)); }
        return "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ApiKeyUser}:{options.ApiKeyPassword}"));
    }

    private readonly HttpClient _httpClient;

    public int PropagationSeconds { get; }

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var response = await _httpClient.GetAsync("domains");

        response.EnsureSuccessStatusCode();

        var domains = await response.Content.ReadAsAsync<DomeneShopDomain[]>();
        var zones = domains.Select(d =>
            new DnsZone(this)
            {
                Id = d.Id,
                Name = d.Domain,
                NameServers = d.Nameservers
            })
            .ToArray();

        return zones;
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        var response = await _httpClient.PostAsync($"domains/{zone.Id}/dns", new { host = relativeRecordName, type = "TXT", ttl = 60, data = string.Join(",", values) });

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        DomeneShopDomainRecord record = await FindRecordAsync(zone, relativeRecordName);

        if (record != null)
        {
            var response = await _httpClient.DeleteAsync($"domains/{zone.Id}/dns/{record.Id}");

            response.EnsureSuccessStatusCode();
        }
    }

    private async Task<DomeneShopDomainRecord> FindRecordAsync(DnsZone zone, string relativeRecordName)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";
        IEnumerable<DomeneShopDomainRecord> records = await ListRecordsAsync(zone);
        return records.Where(r => r.Host == recordName).FirstOrDefault();
    }

    private async Task<IEnumerable<DomeneShopDomainRecord>> ListRecordsAsync(DnsZone zone)
    {
        var response = await _httpClient.GetAsync($"domains/{zone.Id}/dns");

        response.EnsureSuccessStatusCode();

        var records = await response.Content.ReadAsAsync<DomeneShopDomainRecord[]>();

        return records;
    }

    public class DomeneShopDomain
    {
        public string Domain { get; set; }
        public string Id { get; set; }
        public string[] Nameservers { get; set; }
    }

    public class DomeneShopDomainRecord
    {
        public string Id { get; set; }
        public string Host { get; set; }
        public int Ttl { get; set; }
        public string Type { get; set; }
        public string Data { get; set; }
    }
}
