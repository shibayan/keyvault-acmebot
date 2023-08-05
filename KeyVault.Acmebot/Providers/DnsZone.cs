using System;
using System.Collections.Generic;
using System.Globalization;

namespace KeyVault.Acmebot.Providers;

public class DnsZone : IEquatable<DnsZone>
{
    public DnsZone(IDnsProvider dnsProvider)
    {
        DnsProvider = dnsProvider;
    }

    private static readonly IdnMapping s_idnMapping = new();

    private readonly string _name;

    public string Id { get; init; }

    public string Name
    {
        get => _name;
        init => _name = s_idnMapping.GetAscii(value);
    }

    public IReadOnlyList<string> NameServers { get; init; }

    public IDnsProvider DnsProvider { get; }

    public bool Equals(DnsZone other)
    {
        if (other is null)
        {
            return false;
        }

        return Id == other.Id;
    }

    public override bool Equals(object obj) => Equals(obj as DnsZone);

    public override int GetHashCode() => Id?.GetHashCode() ?? 0;
}
