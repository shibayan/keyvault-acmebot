using System;
using System.Collections.Generic;
using System.Linq;

using KeyVault.Acmebot.Models;
using KeyVault.Acmebot.Providers;

namespace KeyVault.Acmebot.Internal;

internal static class DnsZoneExtensions
{
    public static DnsZoneItem ToDnsZoneItem(this DnsZone dnsZone)
    {
        return new DnsZoneItem { Name = dnsZone.Name };
    }

    public static DnsZone FindDnsZone(this IEnumerable<DnsZone> dnsZones, string dnsName)
    {
        return dnsZones.Where(x => string.Equals(dnsName, x.Name, StringComparison.OrdinalIgnoreCase) || dnsName.EndsWith($".{x.Name}", StringComparison.OrdinalIgnoreCase))
                       .MaxBy(x => x.Name.Length);
    }
}
