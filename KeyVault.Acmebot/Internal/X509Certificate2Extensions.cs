using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace KeyVault.Acmebot.Internal;

internal static class X509Certificate2Extensions
{
    private const string AuthorityKeyIdentifierOid = "2.5.29.35";

    public static async Task<X509Certificate2Collection> ReadAsCertificatesAsync(this HttpContent httpContent)
    {
        var certificateData = await httpContent.ReadAsStringAsync();

        var x509Certificates = new X509Certificate2Collection();

        x509Certificates.ImportFromPem(certificateData);

        return x509Certificates;
    }

    /// <summary>
    /// Generates certificate ID by combining AKI and serial number per ARI specification
    /// Format: base64url(AKI) + "." + base64url(SerialNumber)
    /// </summary>
    public static string GenerateCertificateId(this X509Certificate2 certificate)
    {
        try
        {
            // Encode each component separately per RFC draft specification
            var akiEncoded = Base64UrlEncode(certificate.ExtractAuthorityKeyIdentifier());
            var serialEncoded = Base64UrlEncode(certificate.ExtractSerialNumber());

            // Concatenate with dot separator as per ARI specification
            return $"{akiEncoded}.{serialEncoded}";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to generate certificate ID", ex);
        }
    }

    /// <summary>
    /// Encodes byte array using base64url encoding per RFC 4648 Section 5
    /// </summary>
    private static string Base64UrlEncode(byte[] data)
    {   // Convert to base64 then make it URL-safe
        var base64 = Convert.ToBase64String(data);

        // Replace URL-unsafe characters and remove padding
        return base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Extracts Authority Key Identifier from certificate extensions
    /// </summary>
    public static byte[] ExtractAuthorityKeyIdentifier(this X509Certificate2 certificate)
    {
        var akiExtension = certificate.Extensions[AuthorityKeyIdentifierOid];
        if (akiExtension == null)
        {
            throw new InvalidOperationException("Certificate does not contain Authority Key Identifier extension");
        }

        // Parse AKI extension according to RFC 5280
        // AKI is an OCTET STRING containing a SEQUENCE with optional keyIdentifier [0]
        var akiBytes = akiExtension.RawData;

        try
        {
            // Simple parsing assuming keyIdentifier is present as [0] tag
            // Look for tag 0x80 (context-specific, primitive, tag 0)
            for (int i = 0; i < akiBytes.Length - 1; i++)
            {
                if (akiBytes[i] == 0x80) // keyIdentifier tag
                {
                    var length = akiBytes[i + 1];
                    if (i + 2 + length <= akiBytes.Length)
                    {
                        var keyId = new byte[length];
                        Array.Copy(akiBytes, i + 2, keyId, 0, length);
                        return keyId;
                    }
                }
            }

            throw new InvalidOperationException("Cannot parse Authority Key Identifier from certificate extension");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to extract Authority Key Identifier from certificate", ex);
        }
    }

     /// <summary>
    /// Extracts serial number from certificate
    /// </summary>
    public static byte[] ExtractSerialNumber(this X509Certificate2 certificate)
    {
        try
        {
            // Get serial number as byte array, ensuring proper encoding
            var serialString = certificate.SerialNumber;

            // Serial number is returned as hex string, convert to bytes
            // Handle both even and odd length strings
            if (serialString.Length % 2 != 0)
            {
                serialString = "0" + serialString;
            }

            var serialBytes = new byte[serialString.Length / 2];
            for (int i = 0; i < serialBytes.Length; i++)
            {
                serialBytes[i] = Convert.ToByte(serialString.Substring(i * 2, 2), 16);
            }

            return serialBytes;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to extract serial number from certificate", ex);
        }
    }
}
