using System.Collections.Generic;
using System.Threading.Tasks;

namespace KeyVault.Acmebot.Providers
{
    public interface IDnsProvider
    {
        Task<IList<DnsZone>> ListZonesAsync();
        Task UpsertTxtRecordAsync(DnsZone zone, string recordName, IEnumerable<string> values);
    }
}
