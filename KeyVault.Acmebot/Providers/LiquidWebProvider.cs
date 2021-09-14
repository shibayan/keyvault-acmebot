using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using KeyVault.Acmebot.Options;
using Newtonsoft.Json;

namespace KeyVault.Acmebot.Providers
{
    public class LiquidWebProvider : IDnsProvider
    {
        #region Private Members

        private readonly LiquidWebDnsClient _liquidWebDnsClient;

        #endregion

        #region Constructor

        public LiquidWebProvider(LiquidWebOptions options)
        {
            _liquidWebDnsClient = new LiquidWebDnsClient(options.Username, options.Password);
        }

        #endregion

        #region Implement Interface

        public int PropagationSeconds => 60;

        public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values) =>
            await _liquidWebDnsClient.CreateTxtRecordAsync(zone_name: zone.Name, zone_id: zone.Id, relativeRecordName: relativeRecordName, values: values);

        public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName) =>
            await _liquidWebDnsClient.DeleteTxtRecordAsync(zone_name: zone.Name, zone_id: zone.Id, relativeRecordName);

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = await _liquidWebDnsClient.ListZonesAsync();
            return zones.Select(z => new DnsZone { Id = z.Id, Name = z.Name, NameServers = new List<string>() }).ToArray();
        }

        #endregion

        #region Api Client

        private class LiquidWebDnsClient
        {
            #region Private Members

            private readonly string _authHeaderValue;
            private readonly HttpClient _httpClient;

            #endregion

            #region Constructor

            public LiquidWebDnsClient(string username, string password)
            {
                _authHeaderValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));

                _httpClient = new HttpClient
                {
                    BaseAddress = new Uri("https://api.liquidweb.com")
                };

                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _authHeaderValue);
            }

            #endregion

            #region Private Methods

            private async Task<IReadOnlyList<Model.Record>> GetAllRecordsAsync(string zone_id)
            {
                var records = new List<Model.Record>();
                var result = new Model.Result<Model.Record> { PageNumber = 0 };
                do
                {
                    result = await GetRecordsAsync(zone_id, result.PageNumber + 1);
                    records.AddRange(result.Items);
                } while (result.PageNumber < result.PageTotal);
                return records;
            }

            private async Task<Model.Result<Model.Record>> GetRecordsAsync(string zone_id, int page_num)
            {
                var url = $"/v1/Network/DNS/Record/list?zone_id={zone_id}&page_size=50&page_num={page_num}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var serializedResponse = await response.Content.ReadAsStringAsync();
                var deserializedResponse = JsonConvert.DeserializeObject<Model.Result<Model.Record>>(serializedResponse);
                return deserializedResponse;
            }

            private async Task<Model.Result<Model.Zone>> GetZonesAsync(int page_num)
            {
                var url = $"/v1/Network/DNS/Zone/list?page_size=50&page_num={page_num}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var serializedResponse = await response.Content.ReadAsStringAsync();
                var deserializedResponse = JsonConvert.DeserializeObject<Model.Result<Model.Zone>>(serializedResponse);
                return deserializedResponse;
            }

            private async Task CreateRecordAsync(string zone_name, string zone_id, string relativeRecordName, string content, string recordType = "TXT")
            {
                var recordName = $"{relativeRecordName}.{zone_name}";
                var url = $"https://api.liquidweb.com/v1/Network/DNS/Record/create" +
                    $"?zone_id={zone_id}" +
                    $"&name={recordName}" +
                    $"&rdata=\"{content}\"" +
                    $"&type={recordType}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
            }

            private async Task DeleteRecord(string record_id)
            {
                var url = $"https://api.liquidweb.com/v1/Network/DNS/Record/delete?id={record_id}";
                await _httpClient.GetAsync(url);
            }

            #endregion

            #region Public Methods

            public async Task<IReadOnlyList<Model.Zone>> ListZonesAsync()
            {
                var zones = new List<Model.Zone>();
                var result = new Model.Result<Model.Zone> { PageNumber = 0 };
                do
                {
                    result = await GetZonesAsync(result.PageNumber + 1);
                    zones.AddRange(result.Items);
                } while (result.PageNumber < result.PageTotal);
                return zones;
            }

            public async Task CreateTxtRecordAsync(string zone_name, string zone_id, string relativeRecordName, IEnumerable<string> values)
            {
                foreach (var value in values)
                {
                    await CreateRecordAsync(zone_name, zone_id, relativeRecordName, value, "TXT");
                }
            }

            public async Task DeleteTxtRecordAsync(string zone_name, string zone_id, string relativeRecordName)
            {
                var records = await GetAllRecordsAsync(zone_id);
                var matches = records.Where(r => r.IsAcme && r.GetRelativeRecordName(zone_name) == relativeRecordName);
                foreach (var match in matches)
                {
                    await DeleteRecord(match.Id);
                }
            }

            #endregion

            #region Private Api Models

            public class Model
            {
                public class Result<T>
                {
                    [JsonProperty("item_count")]
                    public int Count { get; set; }

                    [JsonProperty("item_total")]
                    public int Total { get; set; }

                    [JsonProperty("items")]
                    public List<T> Items { get; set; }

                    [JsonProperty("page_num")]
                    public int PageNumber { get; set; }

                    [JsonProperty("page_size")]
                    public int PageSize { get; set; }

                    [JsonProperty("page_total")]
                    public int PageTotal { get; set; }
                }

                public class Zone
                {
                    [JsonProperty("active")]
                    public bool Active { get; set; }

                    [JsonProperty("id")]
                    public string Id { get; set; }

                    [JsonProperty("name")]
                    public string Name { get; set; }

                    [JsonProperty("delegation_status")]
                    public string DelegationStatus { get; set; }

                    public bool GoodNameServers => this.DelegationStatus.Equals("CORRECT");
                }

                public class Record
                {
                    [JsonProperty("created")]
                    public string Created { get; set; }

                    [JsonProperty("id")]
                    public string Id { get; set; }

                    [JsonProperty("last_updated")]
                    public string LastUpdated { get; set; }

                    [JsonProperty("name")]
                    public string Name { get; set; }

                    [JsonProperty("prio")]
                    public string Priority { get; set; }

                    [JsonProperty("rdata")]
                    public string RData { get; set; }

                    [JsonProperty("ttl")]
                    public int TTL { get; set; }

                    [JsonProperty("type")]
                    public string Type { get; set; }

                    [JsonProperty("zone_id")]
                    public int ZoneId { get; set; }

                    public bool IsTxt => Type == "TXT";

                    public bool IsAcme => IsTxt && Name.StartsWith("_acme-challenge.");

                    public string GetRelativeRecordName(string zoneName) =>
                        Name.Length == zoneName.Length ? Name : Name.Substring(0, Name.Length - zoneName.Length - 1);
                }
            }

            #endregion
        }

        #endregion
    }
}
