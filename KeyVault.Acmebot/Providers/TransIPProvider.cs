﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;
using KeyVault.Acmebot.Internal;
using KeyVault.Acmebot.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KeyVault.Acmebot.Providers
{
    public class TransIpProvider : IDnsProvider
    {
        public TransIpProvider(AcmebotOptions acmeOptions, TransIpOptions options, TokenCredential credential)
        {
            var keyUri = new Uri(new Uri(acmeOptions.VaultBaseUrl), $"/keys/{options.PrivateKeyName}");
            var cryptoClient = new CryptographyClient(keyUri, credential);
            _transIpClient = new TransIpClient(options.CustomerName, cryptoClient);
        }

        private readonly TransIpClient _transIpClient;

        public int PropagationSeconds => 360;

        public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            var records = values.Select(value => new DnsEntry()
            {
                Name = relativeRecordName,
                Type = "TXT",
                Expire = 60,
                Content = value
            });

            foreach (var record in records)
                await _transIpClient.AddRecord(zone.Name, record);
        }

        public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            var records = await _transIpClient.ListRecords(zone.Name);

            var recordsToDelete = records.Where(r => string.Equals(relativeRecordName, r.Name) && r.Type == "TXT");

            foreach (var record in recordsToDelete)
                await _transIpClient.DeleteRecord(zone.Name, record);
        }

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = await _transIpClient.ListZonesAsync();

            return zones.Select(d => new DnsZone(){ Id = d.Name, Name = d.Name }).ToArray();
        }

        private class TransIpClient
        {
            public TransIpClient(string customerName, CryptographyClient cryptoClient)
            {
                _customerName = customerName;
                _cryptoClient = cryptoClient;

                _httpClient = new HttpClient()
                {
                    BaseAddress = new Uri("https://api.transip.nl/v6/")
                };

                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            private readonly HttpClient _httpClient;
            private readonly string _customerName;
            private readonly CryptographyClient _cryptoClient;
            private TransIPToken _token = null;

            public async Task<IEnumerable<Domain>> ListZonesAsync()
            {
                await EnsureLoggedInAsync();

                var response = await _httpClient.GetAsync("domains");

                response.EnsureSuccessStatusCode();

                var domains = await response.Content.ReadAsAsync<ListDomainsResult>();

                return domains.Domains;
            }

            public async Task<IReadOnlyList<DnsEntry>> ListRecords(string zoneName)
            {
                await EnsureLoggedInAsync();

                HttpResponseMessage response = await _httpClient.GetAsync($"domains/{zoneName}/dns");

                response.EnsureSuccessStatusCode();

                ListDnsEntriesResponse entries = await response.Content.ReadAsAsync<ListDnsEntriesResponse>();

                return entries.DnsEntries.ToList();
            }

            public async Task DeleteRecord(string zoneName, DnsEntry entry)
            {
                await EnsureLoggedInAsync();

                var request = new DnsEntryRequest
                {
                    DnsEntry = entry
                };

                var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"domains/{zoneName}/dns")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json")
                });

                response.EnsureSuccessStatusCode();
            }

            public async Task AddRecord(string zoneName, DnsEntry entry)
            {
                await EnsureLoggedInAsync();

                var request = new DnsEntryRequest
                {
                    DnsEntry = entry
                };

                var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"domains/{zoneName}/dns")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json")
                });

                response.EnsureSuccessStatusCode();
            }

            private async Task EnsureLoggedInAsync()
            {
                if (_token?.IsValid() == true)
                    return;

                if (_token is null)
                {
                    _token = LoadToken();
                    if (_token?.IsValid() == true && _customerName.Equals(_token.CustomerName))
                    {
                        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token.Token);

                        var testResponse = await _httpClient.GetAsync("api-test");

                        if (testResponse.IsSuccessStatusCode)
                            return;
                    }

                }

                await CreateNewToken();
            }

            private async Task CreateNewToken()
            {
                byte[] nonce = new byte[16];
                RandomNumberGenerator.Fill(nonce);

                var request = new TokenRequest
                {
                    Login = _customerName,
                    Nonce = Convert.ToBase64String(nonce)
                };

                (string signature, string body) = await SignRequestAsync(request);

                var response = await new HttpClient().SendAsync(
                    new HttpRequestMessage(HttpMethod.Post, new Uri(_httpClient.BaseAddress, "auth"))
                    {
                        Headers = { { "Signature", signature } },
                        Content = new StringContent(body, Encoding.UTF8, "application/json")
                    });

                response.EnsureSuccessStatusCode();

                var tokenResponse = await response.Content.ReadAsAsync<TokenResponse>();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.Token);

                _token = new TransIPToken()
                {
                    CustomerName = _customerName,
                    Token = tokenResponse.Token,
                    Expires = DateTimeOffset.FromUnixTimeSeconds(tokenResponse.GetTokenExpiration())
                };

                StoreToken(_token);
            }

            private async Task<(string token, string body)> SignRequestAsync(object request)
            {
                string body = JsonConvert.SerializeObject(request);

                using var hasher = SHA512.Create();
                byte[] bytes = hasher.ComputeHash(Encoding.UTF8.GetBytes(body));
                
                SignResult signature = await _cryptoClient.SignAsync(SignatureAlgorithm.RS512, bytes);

                return (Convert.ToBase64String(signature.Signature), body);
            }

            private void StoreToken(TransIPToken token)
            {
                var fullPath = Environment.ExpandEnvironmentVariables(@"%HOME%\.acme\transip_token.json");
                var directoryPath = Path.GetDirectoryName(fullPath);

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var json = JsonConvert.SerializeObject(token, Formatting.Indented);

                File.WriteAllText(fullPath, json);
            }

            private TransIPToken LoadToken()
            {
                var fullPath = Environment.ExpandEnvironmentVariables(@"%HOME%\.acme\transip_token.json");

                if (!File.Exists(fullPath))
                    return null;

                var json = File.ReadAllText(fullPath);

                return JsonConvert.DeserializeObject<TransIPToken>(json);
            }
        }

        private class TransIPToken
        {
            public string CustomerName { get; set; }

            public string Token { get; set; }

            public DateTimeOffset Expires { get; set; }

            public bool IsValid()
            {
                return (!string.IsNullOrEmpty(Token)) && (Expires - DateTimeOffset.Now) > TimeSpan.FromMinutes(1);
            }
        }

        private class TokenResponse
        {
            [JsonProperty("token")]
            public string Token { get; set; }

            public long GetTokenExpiration()
            {
                var token = Token.Split('.')[1];
                token = token.PadRight(token.Length + (4 - token.Length % 4) % 4, '=');

                var tokenBytes = Convert.FromBase64String(token);

                var tokenObject = JObject.Parse(Encoding.UTF8.GetString(tokenBytes));

                return tokenObject.Value<long>("exp");
            }
        }

        private class TokenRequest
        {
            [JsonProperty("login")]
            public string Login { get; set; }

            [JsonProperty("nonce")]
            public string Nonce { get; set; }

            [JsonProperty("read_only")]
            public bool ReadOnly { get; set; } = false;

            [JsonProperty("expiration_time")]
            public string ExpirationTime { get; set; } = "4 weeks";

            [JsonProperty("label")]
            public string Label { get; set; } = "KeyVault.Acmebot." + DateTime.UtcNow.ToString();

            [JsonProperty("global_key")]
            public bool GlobalKey { get; set; } = true;
        }

        private class ListDomainsResult
        {
            [JsonProperty("domains")]
            public IEnumerable<Domain> Domains { get; set; }
        }

        private class Domain
        {
            [JsonProperty("name")]
            public string Name { get; set; }
        }

        private class ListDnsEntriesResponse
        {
            [JsonProperty("dnsEntries")]
            public IEnumerable<DnsEntry> DnsEntries { get; set; }
        }

        private class DnsEntryRequest
        {
            [JsonProperty("dnsEntry")]
            public DnsEntry DnsEntry { get; set; }
        }

        private class DnsEntry
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("expire")]
            public int Expire { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("content")]
            public string Content { get; set; }
        }
    }
}