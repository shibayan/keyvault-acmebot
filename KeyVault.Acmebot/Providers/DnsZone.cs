using System;
using System.Collections.Generic;
using System.Globalization;

namespace KeyVault.Acmebot.Providers
{
    public class DnsZone : IEquatable<DnsZone>
    {
        private readonly IdnMapping _idnMapping = new IdnMapping();

        private string _name;

        public string Id { get; set; }

        public string Name
        {
            get => _name;
            set => _name = _idnMapping.GetAscii(value);
        }

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
