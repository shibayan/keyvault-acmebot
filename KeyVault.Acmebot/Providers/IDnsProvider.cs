using System.Collections.Generic;
using System.Threading.Tasks;

namespace KeyVault.Acmebot.Providers
{
    public interface IDnsProvider
    {
        Task<IReadOnlyList<DnsZone>> ListZonesAsync();
        Task UpsertTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values);
    }
}
