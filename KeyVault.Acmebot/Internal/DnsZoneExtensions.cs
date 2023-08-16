using KeyVault.Acmebot.Models;
using KeyVault.Acmebot.Providers;

namespace KeyVault.Acmebot.Internal;

internal static class DnsZoneExtensions
{
    public static DnsZoneItem ToDnsZoneItem(this DnsZone dnsZone)
    {
        return new DnsZoneItem { Name = dnsZone.Name, DnsProviderName = dnsZone.DnsProvider.Name };
    }
}
