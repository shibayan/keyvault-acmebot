using System;
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
    public class CloudflareProvider : IDnsProvider
    {
        public CloudflareProvider(CloudflareOptions options)
        {
            _cloudflareDnsClient = new CloudflareDnsClient(options.ApiToken);
        }

        private readonly CloudflareDnsClient _cloudflareDnsClient;

        public int PropagationSeconds => 10;

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = await _cloudflareDnsClient.ListAllZonesAsync();

            // Zone API は Punycode されていない値を返すのでエンコードが必要
            return zones.Select(x => new DnsZone { Id = x.Id, Name = x.Name, NameServers = x.NameServers }).ToArray();
        }

        public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            var recordName = $"{relativeRecordName}.{zone.Name}";

            // 必要な検証用の値の数だけ新しく追加する
            foreach (var value in values)
            {
                await _cloudflareDnsClient.CreateDnsRecordAsync(zone.Id, recordName, value);
            }
        }

        public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            var recordName = $"{relativeRecordName}.{zone.Name}";

            var records = await _cloudflareDnsClient.GetDnsRecordsAsync(zone.Id, recordName);

            // 該当する全てのレコードを削除する
            foreach (var record in records)
            {
                await _cloudflareDnsClient.DeleteDnsRecordAsync(zone.Id, record.Id);
            }
        }

        private class CloudflareDnsClient
        {
            public CloudflareDnsClient(string apiToken)
            {
                _httpClient = new HttpClient
                {
                    BaseAddress = new Uri("https://api.cloudflare.com")
                };

                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            }

            private readonly HttpClient _httpClient;

            public async Task<IReadOnlyList<ZoneResult>> ListAllZonesAsync()
            {
                var page = 1;
                var zones = new List<ZoneResult>();

                ApiResult<ZoneResult> result;

                do
                {
                    result = await ListZonesAsync(page);

                    zones.AddRange(result.Result);

                } while (page++ < result.ResultInfo.TotalPages);

                return zones;
            }

            public async Task<IReadOnlyList<DnsRecordResult>> GetDnsRecordsAsync(string zone, string name)
            {
                var response = await _httpClient.GetAsync($"/client/v4/zones/{zone}/dns_records?type=TXT&name={name}&per_page=100");

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsAsync<ApiResult<DnsRecordResult>>();

                return result.Result;
            }

            public async Task CreateDnsRecordAsync(string zone, string name, string content)
            {
                var response = await _httpClient.PostAsync($"/client/v4/zones/{zone}/dns_records", new { type = "TXT", name, content, ttl = 60 });

                response.EnsureSuccessStatusCode();
            }

            public async Task DeleteDnsRecordAsync(string zone, string id)
            {
                var response = await _httpClient.DeleteAsync($"/client/v4/zones/{zone}/dns_records/{id}");

                response.EnsureSuccessStatusCode();
            }

            private async Task<ApiResult<ZoneResult>> ListZonesAsync(int page)
            {
                var response = await _httpClient.GetAsync($"/client/v4/zones?page={page}&per_page=50&status=active");

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsAsync<ApiResult<ZoneResult>>();
            }
        }

        private class ApiResult<T>
        {
            [JsonProperty("result")]
            public T[] Result { get; set; }

            [JsonProperty("result_info")]
            public ResultInfo ResultInfo { get; set; }

            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("errors")]
            public object[] Errors { get; set; }

            [JsonProperty("messages")]
            public object[] Messages { get; set; }
        }

        private class ResultInfo
        {
            [JsonProperty("page")]
            public int Page { get; set; }

            [JsonProperty("per_page")]
            public int PerPage { get; set; }

            [JsonProperty("total_pages")]
            public int TotalPages { get; set; }

            [JsonProperty("count")]
            public int Count { get; set; }

            [JsonProperty("total_count")]
            public int TotalCount { get; set; }
        }

        private class ZoneResult
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }

            [JsonProperty("name_servers")]
            public string[] NameServers { get; set; }
        }

        private class DnsRecordResult
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("content")]
            public string Content { get; set; }

            [JsonProperty("ttl")]
            public int TTL { get; set; }
        }
    }
}
