using System.Globalization;

namespace KeyVault.Acmebot.Internal
{
    public static class Punycode
    {
        private static readonly IdnMapping _idnMapping = new IdnMapping();

        public static string Encode(string unicode)
        {
            return _idnMapping.GetAscii(unicode);
        }
    }
}
