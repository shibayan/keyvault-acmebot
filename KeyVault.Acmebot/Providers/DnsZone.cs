using System;
using System.Collections.Generic;

namespace KeyVault.Acmebot.Providers
{
    public class DnsZone : IEquatable<DnsZone>
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public IReadOnlyList<string> NameServers { get; set; }

        public bool Equals(DnsZone other)
        {
            if (other == null)
            {
                return false;
            }

            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DnsZone);
        }

        public override int GetHashCode() => Id?.GetHashCode() ?? 0;
    }
}
