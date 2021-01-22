using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using KeyVault.Acmebot.Options;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Providers
{
    public class GoDaddyProvider : IDnsProvider
    {
        private readonly GoDaddyClient _goDaddyClient;

        public GoDaddyProvider(GoDaddyOptions options)
        {
            _goDaddyClient = new GoDaddyClient(options.ApiKey, options.Secret);
        }

        public int PropagationSeconds => 60;

        public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            var recordName = $"{relativeRecordName}";

            foreach (var value in values)
            {
                await _goDaddyClient.CreateTxtRecordAsync(zone.Name, recordName, value);
            }
        }

        public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = await _goDaddyClient.ListDomainsAsync();

            return zones.Select(x => new DnsZone { Id = x.domainId.ToString(), Name = x.domain }).ToArray();
        }

        private class GoDaddyClient
        {
            private readonly HttpClient _httpClient;

            public GoDaddyClient(string apiKey, string secret)
            {
                _httpClient = new HttpClient()
                {
                    BaseAddress = new Uri("https://api.godaddy.com/")
                };

                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("sso-key", $"{apiKey}:{secret}");
            }


            public async Task<List<DomainsResult>> ListDomainsAsync()
            {
                var response = await _httpClient.GetAsync($"/v1/domains?statuses=ACTIVE");

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();

                var results = JsonConvert.DeserializeObject<List<DomainsResult>>(result);

                return results;
            }

            public async Task CreateTxtRecordAsync(string domain, string hostname, string textdata)
            {

                //Verify if exists
                var recordsExists = await GetTxtRecordAsync(domain, hostname);

                var content = new List<Record>();
                content.Add(new Record()
                {
                    ttl = 600,
                    name = hostname,
                    type = "TXT",
                    data = textdata
                });

                if (recordsExists)
                {
                    var response = await _httpClient.PutAsJsonAsync($"/v1/domains/{domain}/records/TXT/{hostname}", content);
                    response.EnsureSuccessStatusCode();
                }
                else
                {
                    var stringContent = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PatchAsync($"/v1/domains/{domain}/records", stringContent);
                    response.EnsureSuccessStatusCode();
                }
            }

            public async Task<bool> GetTxtRecordAsync(string domain, string hostname)
            {
                var response = await _httpClient.GetAsync($"/v1/domains/{domain}/records/TXT/{hostname}");

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(result) || result.Length < 10)
                {
                    return false;
                }

                return true;
            }
        }

        public class Record
        {
            public string data { get; set; }
            public string name { get; set; }
            public int ttl { get; set; }
            public string type { get; set; }
        }

        public class DomainsResult
        {
            public DateTime createdAt { get; set; }
            public string domain { get; set; }
            public int domainId { get; set; }
            public bool expirationProtected { get; set; }
            public DateTime expires { get; set; }
            public bool exposeWhois { get; set; }
            public bool holdRegistrar { get; set; }
            public bool locked { get; set; }
            public object nameServers { get; set; }
            public bool privacy { get; set; }
            public bool renewAuto { get; set; }
            public DateTime renewDeadline { get; set; }
            public bool renewable { get; set; }
            public string status { get; set; }
            public DateTime transferAwayEligibleAt { get; set; }
            public bool transferProtected { get; set; }
        }
    }


}
