using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace KeyVault.Acmebot.Internal;

internal static class X509Certificate2Extensions
{
    public static async Task<X509Certificate2Collection> ReadAsCertificatesAsync(this HttpContent httpContent)
    {
        var certificateData = await httpContent.ReadAsStringAsync();

        var x509Certificates = new X509Certificate2Collection();

        x509Certificates.ImportFromPem(certificateData);

        return x509Certificates;
    }
}
