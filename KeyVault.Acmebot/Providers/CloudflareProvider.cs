using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Providers
{
    public class CloudflareProvider : IDnsProvider
    {
        public CloudflareProvider(AcmebotOptions options)
        {
            _cloudflareDnsClient = new CloudflareDnsClient(options.Cloudflare.ApiKey);
        }

        private readonly CloudflareDnsClient _cloudflareDnsClient;

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = await _cloudflareDnsClient.ListAllZonesAsync();

            // Zone API は Punycode されていない値を返すのでエンコードが必要
            return zones.Select(x => new DnsZone { Id = x.Id, Name = Punycode.Encode(x.Name) }).ToArray();
        }

        public async Task UpsertTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            var recordName = $"{relativeRecordName}.{zone.Name}";

            var records = await _cloudflareDnsClient.GetDnsRecordsAsync(zone.Id, recordName);

            // 該当する全てのレコードを一度削除する
            foreach (var record in records)
            {
                await _cloudflareDnsClient.DeleteDnsRecordAsync(zone.Id, record.Id);
            }

            // 必要な検証用の値の数だけ新しく追加しなおす
            foreach (var value in values)
            {
                await _cloudflareDnsClient.CreateDnsRecordAsync(zone.Id, recordName, value);
            }
        }

        public class CloudflareDnsClient
        {
            public CloudflareDnsClient(string apiKey)
            {
                _apiKey = apiKey;
            }

            private readonly string _apiKey;

            private static readonly HttpClient _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.cloudflare.com")
            };

            public async Task<IReadOnlyList<ZoneResult>> ListAllZonesAsync()
            {
                int page = 1;
                var list = new List<ZoneResult>();

                ApiResult<ZoneResult> result;

                do
                {
                    result = await ListZonesAsync(page);

                    list.AddRange(result.Result);

                } while (page < result.ResultInfo.TotalPages);

                return list;
            }

            public async Task<IReadOnlyList<DnsRecordResult>> GetDnsRecordsAsync(string zone, string name)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"/client/v4/zones/{zone}/dns_records?type=TXT&name={name}&per_page=100");

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var response = await _httpClient.SendAsync(request);

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<ApiResult<DnsRecordResult>>(content);

                return result.Result;
            }

            public async Task CreateDnsRecordAsync(string zone, string name, string content)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"/client/v4/zones/{zone}/dns_records");

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var json = JsonConvert.SerializeObject(new { type = "TXT", name, content, ttl = 60 });

                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);

                response.EnsureSuccessStatusCode();
            }

            public async Task DeleteDnsRecordAsync(string zone, string id)
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, $"/client/v4/zones/{zone}/dns_records/{id}");

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var response = await _httpClient.SendAsync(request);

                response.EnsureSuccessStatusCode();
            }

            private async Task<ApiResult<ZoneResult>> ListZonesAsync(int page)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"/client/v4/zones?page={page}&per_page=50&status=active");

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var response = await _httpClient.SendAsync(request);

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<ApiResult<ZoneResult>>(content);

                return result;
            }
        }

        public class ApiResult<T>
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

        public class ResultInfo
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

        public class ZoneResult
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }
        }

        public class DnsRecordResult
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
