using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using KeyVault.Acmebot.Options;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Providers
{
    public class CustomDnsProvider : IDnsProvider
    {
        public CustomDnsProvider(CustomDnsOptions options)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.Endpoint)
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            PropagationSeconds = options.PropagationSeconds;
        }

        private readonly HttpClient _httpClient;

        public int PropagationSeconds { get; }

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var content = await _httpClient.GetStringAsync("zones");

            return JsonConvert.DeserializeObject<DnsZone[]>(content);
        }

        public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            var recordName = $"{relativeRecordName}.{zone.Name}";

            return _httpClient.PostAsJsonAsync("records/create", new { zone.Id, recordName, values });
        }

        public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            var recordName = $"{relativeRecordName}.{zone.Name}";

            return _httpClient.PostAsJsonAsync("records/delete", new { zone.Id, recordName });
        }
    }
}
