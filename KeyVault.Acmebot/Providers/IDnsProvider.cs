using System.Collections.Generic;
using System.Threading.Tasks;

namespace KeyVault.Acmebot.Providers
{
    public interface IDnsProvider
    {
        int PropagationSeconds { get; }
        Task<IReadOnlyList<DnsZone>> ListZonesAsync();
        Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values);
        Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName);
    }
}
