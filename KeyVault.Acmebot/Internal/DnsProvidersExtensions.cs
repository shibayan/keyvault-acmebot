using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using KeyVault.Acmebot.Providers;

namespace KeyVault.Acmebot.Internal;

internal static class DnsProvidersExtensions
{
    public static async Task<IReadOnlyList<DnsZone>> ListAllZonesAsync(this IEnumerable<IDnsProvider> dnsProviders)
    {
        var zones = await Task.WhenAll(dnsProviders.Select(x => x.ListZonesAsync()));

        return zones.SelectMany(x => x).ToArray();
    }
}
