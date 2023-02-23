using System;
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

    public static void TryAdd<TOption>(this IList<IDnsProvider> dnsProviders, TOption options, Func<TOption, IDnsProvider> factory)
    {
        if (options is not null)
        {
            dnsProviders.Add(factory(options));
        }
    }

    public static void TryAdd<TOption>(this IList<IDnsProvider> dnsProviders, TOption[] options, Func<TOption, IDnsProvider> factory)
    {
        if (options is not null)
        {
            foreach (var option in options)
            {
                if (option is not null)
                {
                    dnsProviders.Add(factory(option));
                }
            }
        }
    }
}
