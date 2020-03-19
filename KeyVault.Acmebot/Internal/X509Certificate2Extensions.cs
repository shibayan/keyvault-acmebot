using System;
using System.Security.Cryptography.X509Certificates;

namespace KeyVault.Acmebot.Internal
{
    internal static class X509Certificate2Extensions
    {
        private static ReadOnlySpan<byte> Separator => new byte[] { 0x0A, 0x0A };

        public static void ImportFromPem(this X509Certificate2Collection collection, byte[] rawData)
        {
            var rawDataSpan = rawData.AsSpan();

            var separator = rawDataSpan.IndexOf(Separator);

            collection.Add(new X509Certificate2(rawDataSpan.Slice(0, separator).ToArray()));
            collection.Add(new X509Certificate2(rawDataSpan.Slice(separator + 2).ToArray()));
        }
    }
}
