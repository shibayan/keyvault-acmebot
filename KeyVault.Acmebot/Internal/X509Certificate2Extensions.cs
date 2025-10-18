using System.Buffers.Text;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace KeyVault.Acmebot.Internal;

internal static class X509Certificate2Extensions
{
    public static string GetCertificateId(this X509Certificate2 x509Certificate2)
    {
        var keyIdentifierExtension = x509Certificate2.Extensions.OfType<X509AuthorityKeyIdentifierExtension>().FirstOrDefault();

        if (keyIdentifierExtension is null)
        {
            return null;
        }

        var keyIdentifier = Base64Url.EncodeToString(keyIdentifierExtension.KeyIdentifier.Value.Span);
        var serialNumber = Base64Url.EncodeToString(x509Certificate2.SerialNumberBytes.Span);

        return $"{keyIdentifier}.{serialNumber}";
    }

    public static async Task<X509Certificate2Collection> ReadAsCertificatesAsync(this HttpContent httpContent)
    {
        var certificateData = await httpContent.ReadAsStringAsync();

        var x509Certificates = new X509Certificate2Collection();

        x509Certificates.ImportFromPem(certificateData);

        return x509Certificates;
    }
}
