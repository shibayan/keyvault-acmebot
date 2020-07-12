using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using KeyVault.Acmebot.Options;

namespace KeyVault.Acmebot.Providers
{
    public class CloudflareProvider : IDnsProvider
    {
        public CloudflareProvider(AcmebotOptions options)
        {
        }

        public Task<IList<DnsZone>> ListZonesAsync() => throw new NotImplementedException();

        public Task UpsertTxtRecordAsync(DnsZone zone, string recordName, IEnumerable<string> values) => throw new NotImplementedException();
    }
}
