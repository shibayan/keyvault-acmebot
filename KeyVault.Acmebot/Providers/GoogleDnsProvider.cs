using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Dns.v1;
using Google.Apis.Dns.v1.Data;
using Google.Apis.Json;
using Google.Apis.Services;

using KeyVault.Acmebot.Options;

namespace KeyVault.Acmebot.Providers
{
    public class GoogleDnsProvider : IDnsProvider
    {
        public GoogleDnsProvider(GoogleDnsOptions options)
        {
            var jsonString = Encoding.UTF8.GetString(Convert.FromBase64String(options.KeyFile64));
            var credential = GoogleCredential.FromJson(jsonString).CreateScoped(DnsService.Scope.NdevClouddnsReadwrite);

            // Create the service.
            _dnsService = new DnsService(new BaseClientService.Initializer { HttpClientInitializer = credential });
            _credsParameters = NewtonsoftJsonSerializer.Instance.Deserialize<JsonCredentialParameters>(jsonString);
        }

        private readonly DnsService _dnsService;
        private readonly JsonCredentialParameters _credsParameters;

        public int PropagationSeconds => 60;

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = await _dnsService.ManagedZones.List(_credsParameters.ProjectId).ExecuteAsync();

            return zones.ManagedZones
                        .Select(x => new DnsZone { Id = x.Name, Name = x.DnsName.TrimEnd('.'), NameServers = x.NameServers.ToArray() })
                        .ToArray();
        }

        public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            var recordName = $"{relativeRecordName}.{zone.Name}.";

            var change = new Change
            {
                Additions = new[]
                {
                    new ResourceRecordSet
                    {
                        Name = recordName,
                        Type = "TXT",
                        Ttl = 60,
                        Rrdatas = values.ToArray()
                    }
                }
            };

            return _dnsService.Changes.Create(change, _credsParameters.ProjectId, zone.Id).ExecuteAsync();
        }

        public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            var recordName = $"{relativeRecordName}.{zone.Name}.";

            var request = _dnsService.ResourceRecordSets.List(_credsParameters.ProjectId, zone.Id);

            request.Name = recordName;
            request.Type = "TXT";

            var txtRecords = await request.ExecuteAsync();

            if (txtRecords.Rrsets.Count == 0)
            {
                return;
            }

            var change = new Change { Deletions = txtRecords.Rrsets };

            await _dnsService.Changes.Create(change, _credsParameters.ProjectId, zone.Id).ExecuteAsync();
        }
    }
}
