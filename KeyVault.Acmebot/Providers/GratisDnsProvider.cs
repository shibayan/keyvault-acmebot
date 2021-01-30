using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using KeyVault.Acmebot.Options;

namespace KeyVault.Acmebot.Providers
{
    public class GratisDnsProvider : IDnsProvider
    {
        public GratisDnsProvider(GratisDnsOptions options)
        {
            _gratisDnsClient = new GratisDnsClient(options.Username, options.Password);
        }

        private readonly GratisDnsClient _gratisDnsClient;

        public int PropagationSeconds => 60;

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var domains = await _gratisDnsClient.ListDomainsAsync();

            return domains.Select(x => new DnsZone { Id = x, Name = x }).ToArray();
        }

        public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            var recordName = $"{relativeRecordName}.{zone.Name}";

            foreach (var value in values)
            {
                await _gratisDnsClient.CreateTxtRecordAsync(zone.Name, recordName, value);
            }
        }

        public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            // TODO: Requires implementation
            return Task.CompletedTask;
        }

        private class GratisDnsClient
        {
            public GratisDnsClient(string username, string password)
            {
                _username = username;
                _password = password;

                _httpClient = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() })
                {
                    BaseAddress = new Uri("https://admin.gratisdns.com")
                };
            }

            private bool _isLoggedIn;

            private readonly string _username;
            private readonly string _password;
            private readonly HttpClient _httpClient;

            private async Task LoginAsync()
            {
                var homePageResult = await _httpClient.GetAsync("/");

                homePageResult.EnsureSuccessStatusCode();

                var loginForm = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("action", "logmein"),
                    new KeyValuePair<string, string>("login", _username),
                    new KeyValuePair<string, string>("password", _password),
                });

                var loginResult = await _httpClient.PostAsync("/", loginForm);

                loginResult.EnsureSuccessStatusCode();

                _isLoggedIn = true;
            }

            public async Task<IReadOnlyList<string>> ListDomainsAsync()
            {
                if (!_isLoggedIn)
                {
                    await LoginAsync();
                }

                var primaryDnsResult = await _httpClient.GetAsync("/?action=dns_primarydns");

                primaryDnsResult.EnsureSuccessStatusCode();

                var primaryDnsContent = await primaryDnsResult.Content.ReadAsStringAsync();

                var manageDomainLinks = Regex.Matches(primaryDnsContent, "\"\\?action=dns_primary_changeDNSsetup&user_domain=([^\"]+)\"");

                return manageDomainLinks.Select(x => x.Groups[1].Value).ToArray();
            }

            public async Task CreateTxtRecordAsync(string domain, string hostname, string textdata)
            {
                if (!_isLoggedIn)
                {
                    await LoginAsync();
                }

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("user_domain", domain),
                    new KeyValuePair<string, string>("name", hostname),
                    new KeyValuePair<string, string>("txtdata", textdata),
                    new KeyValuePair<string, string>("ttl", "60"),
                    new KeyValuePair<string, string>("action", "dns_primary_record_added_txt"),
                });

                var response = await _httpClient.PostAsync("/", content);

                response.EnsureSuccessStatusCode();
            }
        }
    }
}
