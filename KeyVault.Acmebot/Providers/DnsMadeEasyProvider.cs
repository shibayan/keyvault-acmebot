using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;

using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KeyVault.Acmebot.Providers
{
    public class DnsMadeEasyProvider : IDnsProvider
    {
        public DnsMadeEasyProvider(AcmebotOptions acmeOptions, DnsMadeEasyOptions options, AzureEnvironment environment)
        {
            _client = new DnsMadeEasyClient(options.ApiKey, options.SecretKey);
        }

        private readonly DnsMadeEasyClient _client;

        public int PropagationSeconds => 10;

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = await _client.ListZonesAsync();

            return zones.Select(x => new DnsZone { Id = x.Id, Name = x.Name }).ToArray();
        }

        public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            foreach (var value in values)
            {
                await _client.AddRecordAsync(zone.Id, new DnsEntry
                {
                    Name = relativeRecordName,
                    Type = "TXT",
                    Expire = 60,
                    Content = value
                });
            }
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

        private class DnsMadeEasyClient
        {
            public DnsMadeEasyClient(string apiKey, string secretKey)
            {
                _httpClient = new HttpClient(new ApiKeyHandler(apiKey, secretKey, new HttpClientHandler()))
                {
                    BaseAddress = new Uri("https://api.dnsmadeeasy.com/V2.0/")
                };

                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            private readonly HttpClient _httpClient;

            public async Task<IReadOnlyList<Domain>> ListZonesAsync()
            {
                var response = await _httpClient.GetAsync("dns/managed");

                response.EnsureSuccessStatusCode();

                var domains = await response.Content.ReadAsAsync<ListDomainsResult>();

                return domains.Domains;
            }

            public async Task<IReadOnlyList<DnsEntry>> ListRecordsAsync(string zoneId)
            {
                var response = await _httpClient.GetAsync($"dns/managed/{zoneId}/records");

                response.EnsureSuccessStatusCode();

                var entries = await response.Content.ReadAsAsync<ListDnsEntriesResponse>();

                return entries.DnsEntries;
            }

            public async Task DeleteRecordAsync(string zoneId, DnsEntry entry)
            {
                var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"dns/managed/{zoneId}/records/{entry.Id}"));

                response.EnsureSuccessStatusCode();
            }

            public async Task AddRecordAsync(string zoneId, DnsEntry entry)
            {
                var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"dns/managed/{zoneId}/records")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(entry), Encoding.UTF8, "application/json")
                });

                response.EnsureSuccessStatusCode();
            }

            internal sealed class ApiKeyHandler : DelegatingHandler
            {
                private string ApiKey { get; }
                private HMACSHA1 HMAC { get; }

                public ApiKeyHandler(string apiKey, string secretKey, HttpMessageHandler innerHandler) : base(innerHandler)
                {
                    if (apiKey is null)
                        throw new ArgumentNullException(nameof(apiKey));
                    if (secretKey is null)
                        throw new ArgumentNullException(nameof(secretKey));
                    if (innerHandler is null)
                        throw new ArgumentNullException(nameof(innerHandler));

                    if (string.IsNullOrWhiteSpace(apiKey))
                        throw new ArgumentException("API Key must be specified", nameof(apiKey));
                    if (string.IsNullOrWhiteSpace(secretKey))
                        throw new ArgumentException("Secret Key must be specified", nameof(secretKey));

                    ApiKey = apiKey;

        #pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms - external specification
                    HMAC = new HMACSHA1(Encoding.UTF8.GetBytes(secretKey));
        #pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
                }

                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var currentTimeStr = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);
                    var hmac = ToHexString(GetHash(Encoding.UTF8.GetBytes(currentTimeStr)));

                    request.Headers.Add("x-dnsme-apikey", ApiKey);
                    request.Headers.Add("x-dnsme-requestdate", currentTimeStr);
                    request.Headers.Add("x-dnsme-hmac", hmac);
                    
                    return base.SendAsync(request, cancellationToken);
                }

                private byte[] GetHash(byte[] bytes)
                {
                    lock(HMAC)
                        return HMAC.ComputeHash(bytes);
                }

                protected override void Dispose(bool disposing)
                {
                    base.Dispose(disposing);
                    if (disposing)
                        HMAC.Dispose();
                }

                // Inspiration: https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa/24343727#24343727
                private static readonly uint[] lookup = { 3145776, 3211312, 3276848, 3342384, 3407920, 3473456, 3538992, 3604528, 3670064, 3735600, 6357040, 6422576, 6488112, 6553648, 6619184, 6684720, 3145777, 3211313, 3276849, 3342385, 3407921, 3473457, 3538993, 3604529, 3670065, 3735601, 6357041, 6422577, 6488113, 6553649, 6619185, 6684721, 3145778, 3211314, 3276850, 3342386, 3407922, 3473458, 3538994, 3604530, 3670066, 3735602, 6357042, 6422578, 6488114, 6553650, 6619186, 6684722, 3145779, 3211315, 3276851, 3342387, 3407923, 3473459, 3538995, 3604531, 3670067, 3735603, 6357043, 6422579, 6488115, 6553651, 6619187, 6684723, 3145780, 3211316, 3276852, 3342388, 3407924, 3473460, 3538996, 3604532, 3670068, 3735604, 6357044, 6422580, 6488116, 6553652, 6619188, 6684724, 3145781, 3211317, 3276853, 3342389, 3407925, 3473461, 3538997, 3604533, 3670069, 3735605, 6357045, 6422581, 6488117, 6553653, 6619189, 6684725, 3145782, 3211318, 3276854, 3342390, 3407926, 3473462, 3538998, 3604534, 3670070, 3735606, 6357046, 6422582, 6488118, 6553654, 6619190, 6684726, 3145783, 3211319, 3276855, 3342391, 3407927, 3473463, 3538999, 3604535, 3670071, 3735607, 6357047, 6422583, 6488119, 6553655, 6619191, 6684727, 3145784, 3211320, 3276856, 3342392, 3407928, 3473464, 3539000, 3604536, 3670072, 3735608, 6357048, 6422584, 6488120, 6553656, 6619192, 6684728, 3145785, 3211321, 3276857, 3342393, 3407929, 3473465, 3539001, 3604537, 3670073, 3735609, 6357049, 6422585, 6488121, 6553657, 6619193, 6684729, 3145825, 3211361, 3276897, 3342433, 3407969, 3473505, 3539041, 3604577, 3670113, 3735649, 6357089, 6422625, 6488161, 6553697, 6619233, 6684769, 3145826, 3211362, 3276898, 3342434, 3407970, 3473506, 3539042, 3604578, 3670114, 3735650, 6357090, 6422626, 6488162, 6553698, 6619234, 6684770, 3145827, 3211363, 3276899, 3342435, 3407971, 3473507, 3539043, 3604579, 3670115, 3735651, 6357091, 6422627, 6488163, 6553699, 6619235, 6684771, 3145828, 3211364, 3276900, 3342436, 3407972, 3473508, 3539044, 3604580, 3670116, 3735652, 6357092, 6422628, 6488164, 6553700, 6619236, 6684772, 3145829, 3211365, 3276901, 3342437, 3407973, 3473509, 3539045, 3604581, 3670117, 3735653, 6357093, 6422629, 6488165, 6553701, 6619237, 6684773, 3145830, 3211366, 3276902, 3342438, 3407974, 3473510, 3539046, 3604582, 3670118, 3735654, 6357094, 6422630, 6488166, 6553702, 6619238, 6684774 };

#if !NETSTANDARD2_1
                public static string ToHexString(byte[] bytes)
                {
                    var lookup32 = lookup;
                    var result = new char[bytes.Length * 2];
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        var val = lookup32[bytes[i]];
                        result[2 * i] = (char)val;
                        result[2 * i + 1] = (char)(val >> 16);
                    }
                    return new string(result);
                }
#else
                public static string ToHexString(byte[] bytes) => string.Create(bytes.Length * 2, bytes, ToString);

                private static void ToHexString(Span<char> chars, byte[] bytes)
                {
                    var lookup32 = lookup;
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        var val = lookup32[bytes[i]];
                        chars[2 * i] = (char)val;
                        chars[2 * i + 1] = (char)(val >> 16);
                    }
                }
#endif
            }
        }

        private class ListDomainsResult
        {
            [JsonProperty("data")]
            public IReadOnlyList<Domain> Domains { get; set; }
        }

        private class Domain
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }
        }

        private class ListDnsEntriesResponse
        {
            [JsonProperty("data")]
            public IReadOnlyList<DnsEntry> DnsEntries { get; set; }
        }

        private class DnsEntry
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("ttl")]
            public int Expire { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("value")]
            public string Content { get; set; }
        }
    }
}
