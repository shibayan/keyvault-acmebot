using System;

using ACMESharp.Crypto.JOSE;

namespace KeyVault.Acmebot.Internal
{
    internal class AccountKey
    {
        public string KeyType { get; set; }
        public string KeyExport { get; set; }

        public IJwsTool GenerateSigner()
        {
            if (KeyType.StartsWith("ES"))
            {
                var tool = new ACMESharp.Crypto.JOSE.Impl.ESJwsTool
                {
                    HashSize = int.Parse(KeyType.Substring(2))
                };
                tool.Init();
                tool.Import(KeyExport);
                return tool;
            }

            if (KeyType.StartsWith("RS"))
            {
                var tool = new ACMESharp.Crypto.JOSE.Impl.RSJwsTool
                {
                    KeySize = int.Parse(KeyType.Substring(2))
                };
                tool.Init();
                tool.Import(KeyExport);
                return tool;
            }

            throw new Exception($"Unknown or unsupported KeyType [{KeyType}]");
        }
    }
}
