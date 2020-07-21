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

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var domains = await _gratisDnsClient.ListDomainsAsync();

            return domains.Select(x => new DnsZone { Id = x, Name = x }).ToArray();
        }

        public async Task UpsertTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            foreach (var value in values)
            {
                await _gratisDnsClient.CreateTxtRecordAsync(zone.Name, $"{relativeRecordName}.{zone.Name}", value);
            }
        }

        private class GratisDnsClient
        {
            private const string BASE_ADDRESS = "https://admin.gratisdns.com";
            private CookieContainer _cookieContainer;
            private HttpClientHandler _handler;
            private HttpClient _client;
            private string _username;
            private string _password;
            private bool _isLoggedIn;

            public GratisDnsClient(string username, string password)
            {
                _username = username;
                _password = password;
                _cookieContainer = new CookieContainer();
                _handler = new HttpClientHandler() { CookieContainer = _cookieContainer };
                _client = new HttpClient(_handler) { BaseAddress = new Uri(BASE_ADDRESS) };
            }

            private async Task LoginAsync()
            {
                var homePageResult = _client.GetAsync("/").Result;
                homePageResult.EnsureSuccessStatusCode();

                var loginForm = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("action", "logmein"),
                    new KeyValuePair<string, string>("login", _username),
                    new KeyValuePair<string, string>("password", _password),
                });

                var loginResult = await _client.PostAsync("/", loginForm);
                loginResult.EnsureSuccessStatusCode();

                _isLoggedIn = true;
            }

            public async Task<string[]> ListDomainsAsync()
            {
                if (!_isLoggedIn)
                {
                    await LoginAsync();
                }

                var primaryDnsResult = await _client.GetAsync("/?action=dns_primarydns");
                primaryDnsResult.EnsureSuccessStatusCode();

                var primaryDnsContent = await primaryDnsResult.Content.ReadAsStringAsync();

                var manageDomainLinks = Regex.Matches(primaryDnsContent, "\"\\?action=dns_primary_changeDNSsetup&user_domain=([^\"]+)\"");

                var domains = new List<string>();

                for (int i = 0; i < manageDomainLinks.Count; i++)
                {
                    domains.Add(manageDomainLinks[i].Groups[1].Value);
                }

                return domains.ToArray();
            }

            public async Task CreateTxtRecordAsync(string domain, string hostname, string textdata)
            {
                if (!_isLoggedIn)
                {
                    await LoginAsync();
                }

                var primaryDnsResult = await _client.GetAsync("/?action=dns_primarydns");
                primaryDnsResult.EnsureSuccessStatusCode();

                var primaryDnsContent = await primaryDnsResult.Content.ReadAsStringAsync();

                var manageDomainLinks = Regex.Matches(primaryDnsContent, "\"\\?action=dns_primary_changeDNSsetup&user_domain=([^\"]+)\"");

                var domains = new List<string>();

                for (int i = 0; i < manageDomainLinks.Count; i++)
                {
                    domains.Add(manageDomainLinks[i].Groups[1].Value);
                }

                var createTxtRecordForm = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("user_domain", domain),
                    new KeyValuePair<string, string>("name", hostname),
                    new KeyValuePair<string, string>("txtdata", textdata),
                    new KeyValuePair<string, string>("ttl", "60"),
                    new KeyValuePair<string, string>("action", "dns_primary_record_added_txt"),
                });

                var createTxtRecordResult = _client.PostAsync("/", createTxtRecordForm).Result;
                createTxtRecordResult.EnsureSuccessStatusCode();
            }
        }
    }
}
