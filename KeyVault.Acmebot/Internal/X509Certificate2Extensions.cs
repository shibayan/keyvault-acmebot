using System;
using System.Security.Cryptography.X509Certificates;

namespace KeyVault.Acmebot.Internal
{
    internal static class X509Certificate2Extensions
    {
        private static ReadOnlySpan<byte> EndCertificate => new byte[]
        {
            0x2d, 0x2d, 0x2d, 0x2d, 0x2d, 0x45, 0x4e, 0x44, 0x20, 0x43,
            0x45, 0x52, 0x54, 0x49, 0x46, 0x49, 0x43, 0x41, 0x54, 0x45,
            0x2d, 0x2d, 0x2d, 0x2d, 0x2d
        };

        public static void ImportFromPem(this X509Certificate2Collection collection, byte[] rawData)
        {
            var rawDataSpan = rawData.AsSpan();

            while (true)
            {
                var foundIndex = rawDataSpan.IndexOf(EndCertificate);

                if (foundIndex == -1)
                {
                    break;
                }

                collection.Add(new X509Certificate2(rawDataSpan.Slice(0, foundIndex + EndCertificate.Length).ToArray()));

                rawDataSpan = rawDataSpan.Slice(foundIndex + EndCertificate.Length);
            }
        }
    }
}
