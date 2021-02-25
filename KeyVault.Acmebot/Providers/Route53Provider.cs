using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Route53;
using Amazon.Route53.Model;
using Amazon.Runtime;

using KeyVault.Acmebot.Options;

namespace KeyVault.Acmebot.Providers
{
    public class Route53Provider : IDnsProvider
    {
        public Route53Provider(Route53Options options)
        {
            _amazonRoute53Client = new AmazonRoute53Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey));
        }

        private readonly AmazonRoute53Client _amazonRoute53Client;

        public int PropagationSeconds => 10;

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = await _amazonRoute53Client.ListHostedZonesAsync();

            return zones.HostedZones.Select(x => new DnsZone { Id = x.Id, Name = x.Name }).ToArray();
        }

        public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            var change = new Change
            {
                Action = ChangeAction.CREATE,
                ResourceRecordSet = new ResourceRecordSet
                {
                    Name = relativeRecordName,
                    Type = RRType.TXT,
                    TTL = 60,
                    ResourceRecords = values.Select(x => new ResourceRecord(x)).ToList()
                }
            };

            var request = new ChangeResourceRecordSetsRequest(zone.Id, new ChangeBatch(new List<Change> { change }));

            await _amazonRoute53Client.ChangeResourceRecordSetsAsync(request);
        }

        public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            var change = new Change
            {
                Action = ChangeAction.DELETE,
                ResourceRecordSet = new ResourceRecordSet
                {
                    Name = relativeRecordName,
                    Type = RRType.TXT,
                    TTL = 60
                }
            };

            var request = new ChangeResourceRecordSetsRequest(zone.Id, new ChangeBatch(new List<Change> { change }));

            await _amazonRoute53Client.ChangeResourceRecordSetsAsync(request);
        }
    }
}
